using TaxMate.Model.Common;
using System.Text.Json;
using TaxMate.Model.Data;
using TaxMate.Model.Documents.Tax;
using TaxMate.Model.DTO.TaxDeclaration;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Interfaces.Documents;
using TaxMate.Service.Mappings;

namespace TaxMate.Service.Services;

public class TaxDeclarationService : ITaxDeclarationService
{
    private readonly ITaxPeriodRepository _taxPeriodRepository;
    private readonly ITaxDeclarationRepository _taxDeclarationRepository;
    private readonly ITaxDeclarationDocumentGenerator _documentGenerator;
    private readonly ITknDeclarationDocumentGenerator _tknDocumentGenerator;
    private readonly IOwnerRevenueProjector? _ownerRevenue;
    
    public TaxDeclarationService(ITaxPeriodRepository taxPeriodRepository,
        ITaxDeclarationRepository taxDeclarationRepository,
        ITaxDeclarationDocumentGenerator documentGenerator,
        ITknDeclarationDocumentGenerator tknDocumentGenerator,
        IOwnerRevenueProjector? ownerRevenue = null)
    {
        _taxPeriodRepository = taxPeriodRepository;
        _taxDeclarationRepository = taxDeclarationRepository;
        _documentGenerator = documentGenerator;
        _tknDocumentGenerator = tknDocumentGenerator;
        _ownerRevenue = ownerRevenue;
    }
    
    public async Task<TaxDeclarationResponse> CreateAsync(
    Guid userId,
    Guid taxPeriodId,
    CreateTaxDeclarationRequest request,
    CancellationToken cancellationToken = default)
{
    var taxPeriod = await _taxPeriodRepository.GetByIdAsync(
        taxPeriodId,
        cancellationToken);

    if (taxPeriod is null)
    {
        throw new NotFoundException("Tax period not found.");
    }

    await EnsureBusinessOwnershipAsync(
        taxPeriod.BusinessId,
        userId,
        cancellationToken);

    if (taxPeriod.Status != TaxPeriodStatuses.Calculated)
    {
        throw new BadRequestException(
            $"Tax period must be in Calculated status. Current status: {taxPeriod.Status}.");
    }

    var existing =
        await _taxDeclarationRepository.GetCurrentByTaxPeriodAsync(
            taxPeriodId,
            cancellationToken);

    if (existing is not null &&
        existing.Status is not TaxDeclarationStatuses.Superseded)
    {
        return MapDeclaration(existing);
    }

    var calculation =
        await _taxDeclarationRepository
            .GetCurrentCalculationWithLinesAsync(
                taxPeriodId,
                cancellationToken);

    if (calculation is null)
    {
        throw new BadRequestException(
            "No current tax calculation exists.");
    }

    var business =
        await _taxPeriodRepository.GetBusinessWithCategoryAsync(
            taxPeriod.BusinessId,
            cancellationToken);

    if (business is null)
    {
        throw new NotFoundException("Business not found.");
    }
    
    if (business.Owner is null)
    {
        throw new BadRequestException(
            "Business owner information is missing.");
    }

    if (string.IsNullOrWhiteSpace(business.Owner.TaxCode))
    {
        throw new BadRequestException(
            "Taxpayer tax code has not been configured.");
    }
    
    var version =
        await _taxDeclarationRepository.GetNextVersionAsync(
            taxPeriodId,
            cancellationToken);

    var formCode = calculation.RecommendedFormCode;

    if (formCode != "01/CNKD" &&
        formCode != "01/TKN-CNKD")
    {
        throw new BadRequestException(
            $"Unsupported tax declaration form: {formCode}.");
    }

    if (formCode == TaxFormCodes.Form01TknCnkd &&
        taxPeriod.PeriodType != TaxPeriodTypes.Tkn)
        throw new BadRequestException(
            "01/TKN-CNKD can only be created from a dedicated TKN period.");

    if (formCode == TaxFormCodes.Form01Cnkd &&
        taxPeriod.PeriodType == TaxPeriodTypes.Tkn)
        throw new BadRequestException(
            "A TKN period cannot create form 01/CNKD.");

    if (formCode == "01/CNKD")
    {
        ValidateTaxPaymentInformation(business);
    }

    var isTaxDeclaration =
        formCode == "01/CNKD";

    var vatTaxAmount =
        isTaxDeclaration
            ? calculation.TotalVatTaxAmount
            : 0m;

    var pitTaxAmount =
        isTaxDeclaration
            ? calculation.TotalPersonalIncomeTaxAmount
            : 0m;

    var totalTaxPayableAmount =
        isTaxDeclaration
            ? calculation.TotalTaxPayableAmount
            : 0m;
    
    var declaration = new TaxDeclaration
    {
        Id = Guid.NewGuid(),

        TaxPeriodId = taxPeriod.Id,
        TaxCalculationId = calculation.Id,

        FormCode = formCode,

        DeclarationCode = BuildDeclarationCode(
            taxPeriod,
            version),

        Version = version,

        DeclarationType = request.DeclarationType,

        SupplementNumber = request.SupplementNumber,

        Status = TaxDeclarationStatuses.Draft,

        TaxpayerName = business.Owner.FullName,

        TaxCode = business.Owner.TaxCode ?? "Chưa cập nhật",

        TaxpayerAddress = business.Address,

        TotalRevenue = calculation.TotalRevenue,

        TotalVatTaxAmount =
            vatTaxAmount,

        TotalPersonalIncomeTaxAmount =
            pitTaxAmount,

        VatExemptionAmount = 0m,

        PersonalIncomeTaxExemptionAmount = 0m,

        VatPayableAmount =
            vatTaxAmount,

        PersonalIncomeTaxPayableAmount =
            pitTaxAmount,

        TotalTaxPayableAmount =
            totalTaxPayableAmount,

        GeneratedAt = DateTime.UtcNow,

        IsCurrent = true,
        
        RemainingPitDeduction =
            calculation.RemainingPitDeduction,

        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    foreach (var source in calculation.Lines
                 .OrderBy(x => x.DisplayOrder))
    {
        declaration.Lines.Add(new TaxDeclarationLine
        {
            Id = Guid.NewGuid(),

            TaxDeclarationId = declaration.Id,

            SectionCode =
                source.SectionCode,

            IndicatorCode =
                source.IndicatorCode,

            BusinessActivityCode =
                source.BusinessActivityCode,

            BusinessActivityName =
                source.BusinessActivityName,

            BusinessLocationId =
                source.BusinessLocationId,

            BusinessLocationCode =
                source.BusinessLocationCode,

            TotalRevenue =
                source.TotalRevenue,

            VatTaxableRevenue =
                source.VatTaxableRevenue,

            VatNonTaxableRevenue =
                source.VatNonTaxableRevenue,

            ZeroRatedVatRevenue =
                source.ZeroRatedVatRevenue,

            VatTaxRate =
                source.VatTaxRate,

            VatTaxAmount =
                source.VatTaxAmount,

            PersonalIncomeTaxableRevenue =
                source.PersonalIncomeTaxableRevenue,

            PersonalIncomeTaxDeductibleRevenue =
                source.PersonalIncomeTaxDeductibleRevenue,

            PersonalIncomeTaxRevenue =
                source.PersonalIncomeTaxRevenue,

            PersonalIncomeTaxRate =
                source.PersonalIncomeTaxRate,

            PersonalIncomeTaxAmount =
                source.PersonalIncomeTaxAmount,

            DisplayOrder =
                source.DisplayOrder,

            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }

    if (formCode == "01/CNKD")
    {
        CreateDefaultObligations(
            declaration,
            taxPeriod,
            business);
    }

    if (formCode == TaxFormCodes.Form01TknCnkd)
    {
        var snapshot = Form01TknCnkd2026SnapshotFactory.Create(
            declaration, calculation, taxPeriod);
        declaration.FormDataJson = JsonSerializer.Serialize(
            snapshot,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    await _taxDeclarationRepository.AddAsync(
        declaration,
        cancellationToken);

    await _taxDeclarationRepository.SaveChangesAsync(
        cancellationToken);

    return MapDeclaration(declaration);
}

    public async Task<TaxDeclarationResponse> GetByIdAsync(
        Guid userId,
        Guid declarationId,
        CancellationToken cancellationToken = default)
    {
        var declaration =
            await _taxDeclarationRepository.GetByIdAsync(
                declarationId,
                cancellationToken);

        if (declaration is null)
        {
            throw new NotFoundException(
                "Tax declaration not found.");
        }

        await EnsureBusinessOwnershipAsync(
            declaration.TaxPeriod.BusinessId,
            userId,
            cancellationToken);

        return MapDeclaration(declaration);
    }

    public async Task<TaxDeclarationResponse> SubmitAsync(
        Guid userId,
        Guid declarationId,
        SubmitTaxDeclarationRequest request,
        CancellationToken cancellationToken = default)
    {
        var declaration =
            await _taxDeclarationRepository.GetByIdAsync(
                declarationId,
                cancellationToken);

        if (declaration is null)
        {
            throw new NotFoundException(
                "Tax declaration not found.");
        }

        await EnsureBusinessOwnershipAsync(
            declaration.TaxPeriod.BusinessId,
            userId,
            cancellationToken);

        if (declaration.Status != TaxDeclarationStatuses.Draft &&
            declaration.Status != TaxDeclarationStatuses.Generated)
        {
            throw new BadRequestException(
                $"Declaration cannot be submitted from status {declaration.Status}.");
        }

        if (declaration.TaxPeriod.Status !=
            TaxPeriodStatuses.Calculated)
        {
            throw new BadRequestException(
                $"Tax period must be in Calculated status. Current status: {declaration.TaxPeriod.Status}.");
        }

        var now = DateTime.UtcNow;

        declaration.Status =
            TaxDeclarationStatuses.Submitted;

        declaration.SubmittedAt = now;

        declaration.SubmissionMethod =
            request.SubmissionMethod;

        declaration.SubmissionReference =
            request.SubmissionReference;

        declaration.UpdatedAt = now;

        declaration.TaxPeriod.Status =
            TaxPeriodStatuses.Submitted;

        declaration.TaxPeriod.SubmittedAt = now;

        declaration.TaxPeriod.UpdatedAt = now;

        await _taxDeclarationRepository.SaveChangesAsync(
            cancellationToken);

        return MapDeclaration(declaration);
    }
    
    private static void CreateDefaultObligations(
        TaxDeclaration declaration,
        TaxPeriod period,
        BusinessProfile business)
    {
        var chapterCode =
            ResolveHouseholdChapterCode(
                business.TaxAuthorityLevel);

        var now = DateTime.UtcNow;

        /*
         * Multi-location:
         * mỗi TaxDeclarationLine giữ doanh thu và thuế của đúng
         * BusinessProfile/location tương ứng.
         *
         * Vì vậy obligation phải được tạo từ từng line,
         * không dùng tổng declaration rồi gắn vào location của anchor business.
         */
        foreach (var line in declaration.Lines
                     .OrderBy(x => x.DisplayOrder))
        {
            var businessLocationCode =
                line.BusinessLocationCode;

            if (line.VatTaxAmount > 0m)
            {
                declaration.Obligations.Add(
                    new TaxDeclarationObligation
                    {
                        Id = Guid.NewGuid(),

                        TaxDeclarationId =
                            declaration.Id,

                        TaxType =
                            TaxTypes.Vat,

                        BusinessLocationCode =
                            businessLocationCode,

                        StateBudgetContent =
                            StateBudgetCodes2026.VatContent,

                        AssessedAmount =
                            line.VatTaxAmount,

                        ExemptionAmount =
                            0m,

                        PayableAmount =
                            line.VatTaxAmount,

                        StateBudgetChapterCode =
                            chapterCode,

                        StateBudgetSubsectionCode =
                            StateBudgetCodes2026.VatSubsection,

                        /*
                         * Các field quản lý ngân sách hiện chưa nằm trên
                         * TaxDeclarationLine, nên vẫn kế thừa từ anchor business.
                         * Bước sau có thể nâng cấp theo từng BusinessProfile
                         * nếu các location thuộc cơ quan thuế khác nhau.
                         */
                        AdministrativeAreaCode =
                            business.TaxAdministrationAreaCode,

                        CollectingAuthority =
                            business.CollectingAuthority,

                        TaxAuthority =
                            business.ManagingTaxAuthority,

                        DueDate =
                            period.DueDate,

                        CreatedAt = now,
                        UpdatedAt = now
                    });
            }

            if (line.PersonalIncomeTaxAmount > 0m)
            {
                declaration.Obligations.Add(
                    new TaxDeclarationObligation
                    {
                        Id = Guid.NewGuid(),

                        TaxDeclarationId =
                            declaration.Id,

                        TaxType =
                            TaxTypes.PersonalIncomeTax,

                        BusinessLocationCode =
                            businessLocationCode,

                        StateBudgetContent =
                            StateBudgetCodes2026.PitBusinessContent,

                        AssessedAmount =
                            line.PersonalIncomeTaxAmount,

                        ExemptionAmount =
                            0m,

                        PayableAmount =
                            line.PersonalIncomeTaxAmount,

                        StateBudgetChapterCode =
                            chapterCode,

                        StateBudgetSubsectionCode =
                            StateBudgetCodes2026.PitBusinessSubsection,

                        AdministrativeAreaCode =
                            business.TaxAdministrationAreaCode,

                        CollectingAuthority =
                            business.CollectingAuthority,

                        TaxAuthority =
                            business.ManagingTaxAuthority,

                        DueDate =
                            period.DueDate,

                        CreatedAt = now,
                        UpdatedAt = now
                    });
            }
        }
    }
    
    private static string BuildDeclarationCode(
        TaxPeriod period,
        int version)
    {
        var periodPart =
            period.PeriodType switch
            {
                TaxPeriodTypes.Quarterly =>
                    $"Q{period.Quarter}",

                TaxPeriodTypes.Monthly =>
                    $"M{period.Month:00}",

                TaxPeriodTypes.Yearly =>
                    "Y",

                TaxPeriodTypes.Tkn =>
                    period.FilingWindow switch
                    {
                        TknFilingWindows.FirstHalf => "TKN-H1",
                        TknFilingWindows.SecondHalf => "TKN-H2",
                        TknFilingWindows.Annual => "TKN-Y",
                        _ => "TKN-UNKNOWN"
                    },

                _ =>
                    "UNKNOWN"
            };

        return $"TK-{period.Year}-{periodPart}-V{version:00}";
    }
    
    private static TaxDeclarationResponse MapDeclaration(
    TaxDeclaration declaration)
{
    return new TaxDeclarationResponse
    {
        Id = declaration.Id,

        TaxPeriodId = declaration.TaxPeriodId,

        TaxCalculationId =
            declaration.TaxCalculationId,

        FormCode =
            declaration.FormCode,

        DeclarationCode =
            declaration.DeclarationCode,

        Version =
            declaration.Version,

        DeclarationType =
            declaration.DeclarationType,

        SupplementNumber =
            declaration.SupplementNumber,

        Status =
            declaration.Status,

        TaxpayerName =
            declaration.TaxpayerName,

        TaxCode =
            declaration.TaxCode,

        TaxpayerAddress =
            declaration.TaxpayerAddress,

        TotalRevenue =
            declaration.TotalRevenue,

        TotalVatTaxAmount =
            declaration.TotalVatTaxAmount,

        TotalPersonalIncomeTaxAmount =
            declaration.TotalPersonalIncomeTaxAmount,

        VatExemptionAmount =
            declaration.VatExemptionAmount,

        PersonalIncomeTaxExemptionAmount =
            declaration.PersonalIncomeTaxExemptionAmount,

        VatPayableAmount =
            declaration.VatPayableAmount,

        PersonalIncomeTaxPayableAmount =
            declaration.PersonalIncomeTaxPayableAmount,

        TotalTaxPayableAmount =
            declaration.TotalTaxPayableAmount,

        GeneratedAt =
            declaration.GeneratedAt,

        SubmittedAt =
            declaration.SubmittedAt,

        Lines = declaration.Lines
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new TaxDeclarationLineResponse
            {
                Id = x.Id,

                SectionCode = x.SectionCode,

                IndicatorCode = x.IndicatorCode,

                BusinessActivityCode = x.BusinessActivityCode,

                BusinessActivityName = x.BusinessActivityName,

                TotalRevenue = x.TotalRevenue,

                VatTaxableRevenue = x.VatTaxableRevenue,

                VatNonTaxableRevenue =
                    x.VatNonTaxableRevenue,

                ZeroRatedVatRevenue =
                    x.ZeroRatedVatRevenue,

                VatTaxRate = x.VatTaxRate,

                VatTaxAmount = x.VatTaxAmount,

                PersonalIncomeTaxableRevenue =
                    x.PersonalIncomeTaxableRevenue,

                PersonalIncomeTaxDeductibleRevenue =
                    x.PersonalIncomeTaxDeductibleRevenue,

                PersonalIncomeTaxRevenue =
                    x.PersonalIncomeTaxRevenue,

                PersonalIncomeTaxRate =
                    x.PersonalIncomeTaxRate,

                PersonalIncomeTaxAmount =
                    x.PersonalIncomeTaxAmount
            })
            .ToList()
    };
}
    
    private async Task EnsureBusinessOwnershipAsync(
        Guid businessId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var belongsToUser =
            await _taxPeriodRepository.BusinessBelongsToUserAsync(
                businessId,
                userId,
                cancellationToken);

        if (!belongsToUser)
        {
            throw new ForbiddenException(
                "You do not have permission to access this business.");
        }
    }
    
    public async Task<TaxDeclarationGeneratedFile> ExportAsync(
        Guid userId,
        Guid declarationId,
        CancellationToken cancellationToken = default)
    {
        var declaration =
            await _taxDeclarationRepository.GetByIdAsync(
                declarationId,
                cancellationToken);

        if (declaration is null)
        {
            throw new NotFoundException(
                "Tax declaration not found.");
        }

        // Giữ ownership check hiện tại của project bạn.
        // Ví dụ nếu TaxPeriod -> Business -> OwnerId:
        if (declaration.TaxPeriod.Business.OwnerId != userId)
        {
            throw new ForbiddenException(
                "You do not have permission to access this tax declaration.");
        }

        if (declaration.FormCode == TaxFormCodes.Form01TknCnkd)
        {
            if (string.IsNullOrWhiteSpace(declaration.FormDataJson))
                throw new BadRequestException(
                    "The TKN declaration has no immutable form snapshot and cannot be exported.");

            Form01TknCnkd2026Snapshot snapshot;
            try
            {
                snapshot = JsonSerializer.Deserialize<Form01TknCnkd2026Snapshot>(
                    declaration.FormDataJson,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? throw new JsonException("TKN snapshot is empty.");
            }
            catch (JsonException exception)
            {
                throw new BadRequestException(
                    $"The TKN declaration snapshot is invalid: {exception.Message}");
            }

            return await _tknDocumentGenerator.GenerateAsync(
                snapshot,
                cancellationToken);
        }

        if (declaration.FormCode != TaxFormCodes.Form01Cnkd)
        {
            throw new BadRequestException(
                $"Export for form {declaration.FormCode} is not supported yet.");
        }

        if (declaration.Lines.Count == 0)
        {
            throw new BadRequestException(
                "Tax declaration does not contain calculation lines.");
        }

        var ownerBusinesses =
            await _taxPeriodRepository
                .GetBusinessesWithCategoriesByOwnerAsync(
                    declaration.TaxPeriod.Business.OwnerId,
                    cancellationToken);

        var formModel =
            Form01Cnkd2026Mapper.Map(
                declaration,
                ownerBusinesses);

        return await _documentGenerator.GenerateAsync(
            formModel,
            cancellationToken);
    }

    public async Task<TaxDeclarationResponse?> GetByTaxPeriodAsync(
        Guid userId,
        Guid taxPeriodId,
        CancellationToken cancellationToken = default)
    {
        var taxPeriod = await _taxPeriodRepository.GetByIdAsync(
            taxPeriodId,
            cancellationToken);

        if (taxPeriod is null)
        {
            throw new NotFoundException(
                "Tax period not found.");
        }

        await EnsureBusinessOwnershipAsync(
            taxPeriod.BusinessId,
            userId,
            cancellationToken);

        var declaration =
            await _taxDeclarationRepository
                .GetCurrentByTaxPeriodAsync(
                    taxPeriodId,
                    cancellationToken);

        if (declaration is null)
        {
            return null;
        }

        return MapDeclaration(declaration);
    }

    public async Task<TaxDeclarationGeneratedFile> ExportPreviewAsync(
        Guid userId,
        Guid taxPeriodId,
        CancellationToken cancellationToken = default)
    {
        var taxPeriod = await _taxPeriodRepository.GetByIdAsync(
            taxPeriodId,
            cancellationToken);

        if (taxPeriod is null)
        {
            throw new NotFoundException("Tax period not found.");
        }

        await EnsureBusinessOwnershipAsync(
            taxPeriod.BusinessId,
            userId,
            cancellationToken);

        var existing = await _taxDeclarationRepository.GetCurrentByTaxPeriodAsync(
            taxPeriodId,
            cancellationToken);

        if (existing is not null &&
            existing.Status is not TaxDeclarationStatuses.Superseded)
        {
            return await ExportAsync(userId, existing.Id, cancellationToken);
        }

        var business = await _taxPeriodRepository.GetBusinessWithCategoryAsync(
            taxPeriod.BusinessId,
            cancellationToken) ?? throw new NotFoundException("Business not found.");

        var ownerBusinesses = await _taxPeriodRepository
            .GetBusinessesWithCategoriesByOwnerAsync(
                business.OwnerId,
                cancellationToken);

        var calculation = await _taxDeclarationRepository
            .GetCurrentCalculationWithLinesAsync(
                taxPeriodId,
                cancellationToken);

        if (taxPeriod.PeriodType == TaxPeriodTypes.Tkn)
        {
            var selector = taxPeriod.FilingWindow switch
            {
                TknFilingWindows.FirstHalf => "FirstHalf",
                TknFilingWindows.SecondHalf => "SecondHalf",
                _ => "Year"
            };

            List<Form01TknCnkd2026LineSnapshot> sectionALines;
            decimal annualRevenue;

            if (calculation is not null && calculation.Lines.Count > 0)
            {
                annualRevenue = calculation.AnnualRevenueAtCalculation > 0m
                    ? calculation.AnnualRevenueAtCalculation
                    : calculation.TotalRevenue;

                sectionALines = calculation.Lines.Select(x => new Form01TknCnkd2026LineSnapshot(
                    x.SectionCode ?? "I",
                    "08",
                    x.BusinessActivityCode ?? "HD1",
                    x.BusinessActivityName ?? "Kinh doanh",
                    business.Id,
                    business.BusinessLocationCode,
                    x.TotalRevenue,
                    x.VatNonTaxableRevenue,
                    x.ZeroRatedVatRevenue,
                    x.VatTaxAmount,
                    x.PersonalIncomeTaxableRevenue,
                    x.PersonalIncomeTaxDeductibleRevenue,
                    x.PersonalIncomeTaxAmount,
                    x.DisplayOrder)).ToList();
            }
            else if (_ownerRevenue is not null)
            {
                var projection = await _ownerRevenue.ProjectAsync(
                    userId,
                    taxPeriod.BusinessId,
                    taxPeriod.PeriodStartDate,
                    taxPeriod.PeriodEndDate,
                    cancellationToken);

                annualRevenue = projection.TotalRevenue;
                var order = 1;
                sectionALines = projection.Groups
                    .OrderBy(x => x.BusinessCategoryCode)
                    .Select(g => new Form01TknCnkd2026LineSnapshot(
                        "I",
                        "08",
                        g.BusinessCategoryCode,
                        g.BusinessCategoryName,
                        business.Id,
                        business.BusinessLocationCode,
                        g.TotalRevenue,
                        g.TotalRevenue,
                        0m,
                        0m,
                        0m,
                        0m,
                        0m,
                        order++
                    )).ToList();
            }
            else
            {
                annualRevenue = taxPeriod.TotalRevenue;
                sectionALines = [];
            }

            var tknSnapshot = new Form01TknCnkd2026Snapshot
            {
                DeclarationId = Guid.Empty,
                DeclarationCode = $"01-TKN-PREVIEW-{taxPeriod.Year}",
                DeclarationVersion = 1,
                DeclarationType = "Initial",
                SupplementNumber = null,
                GeneratedAt = DateTime.UtcNow,
                PeriodSelector = selector,
                Year = taxPeriod.Year,
                WindowStart = taxPeriod.PeriodStartDate,
                WindowEnd = taxPeriod.PeriodEndDate,
                DueDate = taxPeriod.DueDate,
                IsNewBusinessAtOrBelowOneBillion = selector != "Year",
                TaxpayerName = business.Owner?.FullName ?? business.BusinessName,
                TaxCode = business.Owner?.TaxCode ?? "0123456789",
                TaxpayerAddress = business.Address ?? "Địa chỉ kinh doanh",
                AnnualRevenueAtGeneration = annualRevenue,
                ApplicableThreshold = 100000000m,
                CalculationRuleVersion = "2026.01",
                SectionALines = sectionALines
            };

            var file = await _tknDocumentGenerator.GenerateAsync(
                tknSnapshot,
                cancellationToken);

            return new TaxDeclarationGeneratedFile
            {
                Content = file.Content,
                FileName = $"01-TKN-CNKD_XEM-TRUOC_{taxPeriod.Year}.docx",
                ContentType = file.ContentType
            };
        }

        var mockDeclaration = new TaxDeclaration
        {
            Id = Guid.Empty,
            TaxPeriodId = taxPeriod.Id,
            TaxPeriod = taxPeriod,
            FormCode = TaxFormCodes.Form01Cnkd,
            DeclarationCode = $"01-CNKD-PREVIEW-{taxPeriod.Year}-Q{taxPeriod.Quarter}",
            Version = 1,
            DeclarationType = TaxDeclarationTypes.Initial,
            Status = TaxDeclarationStatuses.Draft,
            TaxpayerName = business.BusinessName,
            TaxCode = business.Owner?.TaxCode ?? "0123456789",
            TaxpayerAddress = business.Address ?? "Địa chỉ kinh doanh",
            TotalRevenue = calculation?.TotalRevenue ?? taxPeriod.TotalRevenue,
            TotalVatTaxAmount = calculation?.TotalVatTaxAmount ?? 0m,
            TotalPersonalIncomeTaxAmount = calculation?.TotalPersonalIncomeTaxAmount ?? 0m,
            VatExemptionAmount = 0m,
            PersonalIncomeTaxExemptionAmount = 0m,
            VatPayableAmount = calculation?.TotalVatTaxAmount ?? 0m,
            PersonalIncomeTaxPayableAmount = calculation?.TotalPersonalIncomeTaxAmount ?? 0m,
            TotalTaxPayableAmount = calculation?.TotalTaxPayableAmount ?? 0m,
            GeneratedAt = DateTime.UtcNow,
            Lines = calculation?.Lines.Select(source => new TaxDeclarationLine
            {
                Id = Guid.NewGuid(),
                SectionCode = source.SectionCode,
                IndicatorCode = source.IndicatorCode,
                BusinessActivityCode = source.BusinessActivityCode,
                BusinessActivityName = source.BusinessActivityName,
                BusinessLocationId = source.BusinessLocationId,
                BusinessLocationCode = source.BusinessLocationCode,
                TotalRevenue = source.TotalRevenue,
                VatTaxableRevenue = source.VatTaxableRevenue,
                VatNonTaxableRevenue = source.VatNonTaxableRevenue,
                ZeroRatedVatRevenue = source.ZeroRatedVatRevenue,
                VatTaxRate = source.VatTaxRate,
                VatTaxAmount = source.VatTaxAmount,
                PersonalIncomeTaxableRevenue = source.PersonalIncomeTaxableRevenue,
                PersonalIncomeTaxDeductibleRevenue = source.PersonalIncomeTaxDeductibleRevenue,
                PersonalIncomeTaxRevenue = source.PersonalIncomeTaxRevenue,
                PersonalIncomeTaxRate = source.PersonalIncomeTaxRate,
                PersonalIncomeTaxAmount = source.PersonalIncomeTaxAmount,
                DisplayOrder = source.DisplayOrder,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList() ?? []
        };

        var formModel = Form01Cnkd2026Mapper.Map(
            mockDeclaration,
            ownerBusinesses);

        var docx = await _documentGenerator.GenerateAsync(
            formModel,
            cancellationToken);

        return new TaxDeclarationGeneratedFile
        {
            Content = docx.Content,
            FileName = $"01-CNKD_XEM-TRUOC_{taxPeriod.Year}_Q{taxPeriod.Quarter}.docx",
            ContentType = docx.ContentType
        };
    }
    
    private static string ResolveHouseholdChapterCode(
        string? taxAuthorityLevel)
    {
        return taxAuthorityLevel switch
        {
            TaxAuthorityLevels.Province =>
                StateBudgetCodes2026.HouseholdProvinceChapter,

            TaxAuthorityLevels.Local =>
                StateBudgetCodes2026.HouseholdLocalChapter,

            _ => throw new BadRequestException(
                "Tax authority level has not been configured for the business.")
        };
    }
    
    private static void ValidateTaxPaymentInformation(
        BusinessProfile business)
    {
        if (string.IsNullOrWhiteSpace(
                business.TaxAuthorityLevel))
        {
            throw new BadRequestException(
                "Tax authority level has not been configured.");
        }

        if (!TaxAuthorityLevels.All.Contains(
                business.TaxAuthorityLevel))
        {
            throw new BadRequestException(
                $"Invalid tax authority level: {business.TaxAuthorityLevel}.");
    }

        if (string.IsNullOrWhiteSpace(
                business.TaxAdministrationAreaCode))
        {
            throw new BadRequestException(
                "Tax administration area code has not been configured.");
        }

        if (string.IsNullOrWhiteSpace(
                business.ManagingTaxAuthority))
        {
            throw new BadRequestException(
                "Managing tax authority has not been configured.");
        }

        if (string.IsNullOrWhiteSpace(
                business.CollectingAuthority))
        {
            throw new BadRequestException(
                "Collecting authority has not been configured.");
        }
    }
}
