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
    
    public TaxDeclarationService(ITaxPeriodRepository taxPeriodRepository,
        ITaxDeclarationRepository taxDeclarationRepository,
        ITaxDeclarationDocumentGenerator documentGenerator,
        ITknDeclarationDocumentGenerator tknDocumentGenerator)
    {
        _taxPeriodRepository = taxPeriodRepository;
        _taxDeclarationRepository = taxDeclarationRepository;
        _documentGenerator = documentGenerator;
        _tknDocumentGenerator = tknDocumentGenerator;
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

    public async Task<TaxDeclarationResponse> GetByTaxPeriodAsync(
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
            throw new NotFoundException(
                "Tax declaration not found.");
        }

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
