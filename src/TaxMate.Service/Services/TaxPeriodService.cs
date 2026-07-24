using TaxMate.Model.Common;
using TaxMate.Model.DTO.TaxPeriod;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;
using BadRequestException = TaxMate.Service.Exceptions.BadRequestException;
using NotFoundException = TaxMate.Service.Exceptions.NotFoundException;
using TaxMate.Service.Exceptions;

namespace TaxMate.Service.Services;

public class TaxPeriodService : ITaxPeriodService
{
    private readonly ITaxPeriodRepository _taxPeriodRepository;
    private readonly ITaxCalculationRepository _taxCalculationRepository;

    public TaxPeriodService(
        ITaxPeriodRepository taxPeriodRepository,
        ITaxCalculationRepository taxCalculationRepository)
    {
        _taxPeriodRepository = taxPeriodRepository;
        _taxCalculationRepository = taxCalculationRepository;
    }

    public async Task<IReadOnlyList<TaxPeriodSummaryResponse>>
        GetByBusinessAsync(
            Guid userId,
            Guid businessId,
            GetTaxPeriodsRequest request,
            CancellationToken cancellationToken = default)
    {
        await EnsureBusinessOwnershipAsync(
            businessId,
            userId,
            cancellationToken);

        ValidateRequest(request);

        return await _taxPeriodRepository.GetByBusinessAsync(
            businessId,
            request,
            cancellationToken);
    }

    public async Task<TaxPeriodDetailResponse> GetByIdAsync(
        Guid userId,
        Guid taxPeriodId,
        CancellationToken cancellationToken = default)
    {
        var taxPeriod = await _taxPeriodRepository.GetDetailAsync(
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

        return taxPeriod;
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

    private static void ValidateRequest(GetTaxPeriodsRequest request)
    {
        if (request.Year.HasValue &&
            request.Year.Value is < 2000 or > 2100)
        {
            throw new BadRequestException(
                "Year must be between 2000 and 2100.");
        }

        if (!string.IsNullOrWhiteSpace(request.PeriodType) &&
            !TaxPeriodTypes.All.Contains(request.PeriodType))
        {
            throw new BadRequestException(
                $"Invalid tax period type: {request.PeriodType}.");
        }

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            !TaxPeriodStatuses.All.Contains(request.Status))
        {
            throw new BadRequestException(
                $"Invalid tax period status: {request.Status}.");
        }
    }
    
    public async Task<TaxPeriodPreviewResponse> GetPreviewAsync(
        Guid userId,
        Guid taxPeriodId,
        CancellationToken cancellationToken = default)
    {
        var preview = await _taxPeriodRepository.GetPreviewAsync(
            taxPeriodId,
            cancellationToken);

        if (preview is null)
        {
            throw new NotFoundException("Tax period not found.");
        }

        await EnsureBusinessOwnershipAsync(
            preview.BusinessId,
            userId,
            cancellationToken);

        return preview;
    }
    
    public async Task<CloseTaxPeriodResponse> CloseAsync(
    Guid userId,
    Guid taxPeriodId,
    CloseTaxPeriodRequest request,
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

    if (taxPeriod.Status != TaxPeriodStatuses.Open)
    {
        throw new BadRequestException(
            $"Tax period must be in Open status. Current status: {taxPeriod.Status}.");
    }

    var preview = await _taxPeriodRepository.GetPreviewAsync(
        taxPeriodId,
        cancellationToken);

    if (preview is null)
    {
        throw new NotFoundException("Tax period preview not found.");
    }

    if (!preview.CanClose)
    {
        throw new BadRequestException(
            "Tax period cannot be closed because no transaction data exists.");
    }

    if (preview.Warnings.Count > 0 &&
        !request.ConfirmWarnings)
    {
        throw new ConflictException(
            "Tax period contains warnings. Confirm warnings before closing.");
    }

    taxPeriod.SalesRevenue = preview.SalesRevenue;
    taxPeriod.OtherRevenue = preview.OtherRevenue;
    taxPeriod.TotalRevenue = preview.TotalRevenue;
    taxPeriod.TaxableRevenue = preview.TaxableRevenue;

    taxPeriod.Status = TaxPeriodStatuses.Closed;
    taxPeriod.ClosedAt = DateTime.UtcNow;
    taxPeriod.UpdatedAt = DateTime.UtcNow;

    await _taxPeriodRepository.SaveChangesAsync(
        cancellationToken);

    return new CloseTaxPeriodResponse
    {
        TaxPeriodId = taxPeriod.Id,
        Status = taxPeriod.Status,

        SalesRevenue = taxPeriod.SalesRevenue,
        OtherRevenue = taxPeriod.OtherRevenue,
        TotalRevenue = taxPeriod.TotalRevenue,
        TaxableRevenue = taxPeriod.TaxableRevenue,

        ClosedAt = taxPeriod.ClosedAt.Value
    };
}
    
    public async Task<TaxCalculationResponse> CalculateAsync(
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

    if (taxPeriod.Status != TaxPeriodStatuses.Closed)
    {
        throw new BadRequestException(
            $"Tax period must be in Closed status. Current status: {taxPeriod.Status}.");
    }

    var business =
        await _taxPeriodRepository.GetBusinessWithCategoryAsync(
            taxPeriod.BusinessId,
            cancellationToken);

    if (business is null)
    {
        throw new NotFoundException("Business not found.");
    }

    if (business.MainCategory is null)
    {
        throw new BadRequestException(
            "Business category is required before tax calculation.");
    }

    var category = business.MainCategory;

    var taxableRevenue = taxPeriod.TaxableRevenue;

    
    var vatNonTaxableRevenue = 0m;
    var zeroRatedVatRevenue = 0m;

    var previousAnnualRevenue =
        await _taxPeriodRepository
            .GetAnnualRevenueBeforePeriodByOwnerAsync(
                business.OwnerId,
                taxPeriod.Year,
                taxPeriod.PeriodStartDate,
                cancellationToken);

    var alreadyConsumedDeduction =
        Math.Min(
            previousAnnualRevenue,
            TaxRules.AnnualPitRevenueDeduction2026);

    var remainingDeduction =
        Math.Max(
            0m,
            TaxRules.AnnualPitRevenueDeduction2026
            - alreadyConsumedDeduction);
    
    var pitTaxableRevenue = taxableRevenue;

    var pitDeductibleRevenue = Math.Min(
        pitTaxableRevenue,
        remainingDeduction);
    
    var remainingPitDeductionAfterPeriod =
        Math.Max(
            0m,
            remainingDeduction -
            pitDeductibleRevenue);

    var pitRevenue = Math.Max(
        0m,
        pitTaxableRevenue - pitDeductibleRevenue);
    
    var vatTaxAmount = decimal.Round(
        taxableRevenue * category.VatRate / 100m,
        2,
        MidpointRounding.AwayFromZero);

    var pitTaxAmount = decimal.Round(
        pitRevenue *
        category.PitRate /
        100m,
        2,
        MidpointRounding.AwayFromZero);

    var totalTaxBeforeExemption =
        vatTaxAmount + pitTaxAmount;

    var totalExemptionAmount = 0m;

    var totalTaxPayableAmount =
        totalTaxBeforeExemption - totalExemptionAmount;

    await _taxPeriodRepository
        .SetPreviousCalculationsAsSupersededAsync(
            taxPeriod.Id,
            cancellationToken);

    var version =
        await _taxPeriodRepository.GetNextCalculationVersionAsync(
            taxPeriod.Id,
            cancellationToken);
    
    var annualRevenue =
        await _taxPeriodRepository
            .GetAnnualRevenueByOwnerAsync(
                business.OwnerId,
                taxPeriod.Year,
                cancellationToken);

    var recommendedFormCode =
        annualRevenue >
        TaxRules.AnnualRevenueThreshold2026
            ? "01/CNKD"
            : "01/TKN-CNKD";

    var calculation = new TaxCalculation
    {
        Id = Guid.NewGuid(),

        TaxPeriodId = taxPeriod.Id,

        Version = version,

        Status = TaxCalculationStatuses.Completed,

        TotalRevenue = taxPeriod.TotalRevenue,

        TotalTaxableRevenue = taxableRevenue,

        TotalVatTaxAmount = vatTaxAmount,

        TotalPersonalIncomeTaxAmount = pitTaxAmount,

        TotalTaxBeforeExemption =
            totalTaxBeforeExemption,

        TotalExemptionAmount =
            totalExemptionAmount,

        TotalTaxPayableAmount =
            totalTaxPayableAmount,

        CalculatedAt = DateTime.UtcNow,

        CalculatedByUserId = userId,

        AnnualRevenueAtCalculation = annualRevenue,
        
        ApplicableRevenueThreshold = TaxRules.AnnualRevenueThreshold2026,
        
        RecommendedFormCode = recommendedFormCode,
        
        RemainingPitDeduction =
            remainingPitDeductionAfterPeriod,
        
        IsCurrent = true,

        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
    
    var line = new TaxCalculationLine
    {
        Id = Guid.NewGuid(),

        TaxCalculationId = calculation.Id,

        BusinessCategoryId =
            category.BusinessCategoryId,

        SectionCode =
            category.FormSectionCode ?? "I",

        IndicatorCode =
            category.FormIndicatorCode ?? "d",

        BusinessActivityCode =
            category.Code,

        BusinessActivityName =
            category.Name,

        TotalRevenue =
            taxPeriod.TotalRevenue,

        VatTaxableRevenue =
            taxableRevenue,

        VatNonTaxableRevenue =
            vatNonTaxableRevenue,

        ZeroRatedVatRevenue =
            zeroRatedVatRevenue,

        VatTaxRate =
            category.VatRate,

        VatTaxAmount =
            vatTaxAmount,

        PersonalIncomeTaxableRevenue =
            pitTaxableRevenue,

        PersonalIncomeTaxDeductibleRevenue =
            pitDeductibleRevenue,

        PersonalIncomeTaxRevenue =
            pitRevenue,

        PersonalIncomeTaxRate =
            category.PitRate,

        PersonalIncomeTaxAmount =
            pitTaxAmount,

        DisplayOrder = 1,

        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    calculation.Lines.Add(line);

    taxPeriod.VatTaxAmount =
        vatTaxAmount;

    taxPeriod.PersonalIncomeTaxAmount =
        pitTaxAmount;

    taxPeriod.EstimatedTax =
        totalTaxPayableAmount;

    taxPeriod.TaxAmountDebt =
        totalTaxPayableAmount;

    taxPeriod.Status =
        TaxPeriodStatuses.Calculated;

    taxPeriod.CalculatedAt =
        DateTime.UtcNow;

    taxPeriod.UpdatedAt =
        DateTime.UtcNow;

    await _taxCalculationRepository.AddAsync(calculation);

    await _taxPeriodRepository.SaveChangesAsync(
        cancellationToken);

    return new TaxCalculationResponse
    {
        TaxPeriodId = taxPeriod.Id,

        TaxCalculationId =
            calculation.Id,

        Version =
            calculation.Version,

        TotalRevenue =
            calculation.TotalRevenue,

        TotalTaxableRevenue =
            calculation.TotalTaxableRevenue,

        TotalVatTaxAmount =
            calculation.TotalVatTaxAmount,

        TotalPersonalIncomeTaxAmount =
            calculation.TotalPersonalIncomeTaxAmount,

        TotalTaxBeforeExemption =
            calculation.TotalTaxBeforeExemption,

        TotalExemptionAmount =
            calculation.TotalExemptionAmount,

        TotalTaxPayableAmount =
            calculation.TotalTaxPayableAmount,

        Status =
            taxPeriod.Status,

        CalculatedAt =
            calculation.CalculatedAt,

        Lines =
        [
            new TaxCalculationLineResponse
            {
                Id = line.Id,

                BusinessCategoryId =
                    line.BusinessCategoryId,

                SectionCode =
                    line.SectionCode,

                IndicatorCode =
                    line.IndicatorCode,

                BusinessActivityCode =
                    line.BusinessActivityCode,

                BusinessActivityName =
                    line.BusinessActivityName,

                TotalRevenue =
                    line.TotalRevenue,

                VatTaxableRevenue =
                    line.VatTaxableRevenue,

                VatNonTaxableRevenue =
                    line.VatNonTaxableRevenue,

                ZeroRatedVatRevenue =
                    line.ZeroRatedVatRevenue,

                VatTaxRate =
                    line.VatTaxRate,

                VatTaxAmount =
                    line.VatTaxAmount,

                PersonalIncomeTaxableRevenue =
                    line.PersonalIncomeTaxableRevenue,

                PersonalIncomeTaxDeductibleRevenue =
                    line.PersonalIncomeTaxDeductibleRevenue,

                PersonalIncomeTaxRevenue =
                    line.PersonalIncomeTaxRevenue,

                PersonalIncomeTaxRate =
                    line.PersonalIncomeTaxRate,

                PersonalIncomeTaxAmount =
                    line.PersonalIncomeTaxAmount
            }
        ]
    };
}
}