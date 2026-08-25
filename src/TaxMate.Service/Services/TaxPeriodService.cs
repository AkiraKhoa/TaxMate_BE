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
    private readonly ITaxPolicyService _taxPolicyService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccountingTransactionLockRepository _transactionLock;
    private readonly IS2eBookProjector _s2eProjector;

    public TaxPeriodService(
        ITaxPeriodRepository taxPeriodRepository,
        ITaxCalculationRepository taxCalculationRepository,
        ITaxPolicyService taxPolicyService,
        IUnitOfWork unitOfWork,
        IAccountingTransactionLockRepository transactionLock,
        IS2eBookProjector s2eProjector)
    {
        _taxPeriodRepository = taxPeriodRepository;
        _taxCalculationRepository = taxCalculationRepository;
        _taxPolicyService = taxPolicyService;
        _unitOfWork = unitOfWork;
        _transactionLock = transactionLock;
        _s2eProjector = s2eProjector;
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

        // Repository resolves businessId -> OwnerId and returns only
        // one canonical TaxPeriod for each owner-level tax period.
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
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Identity and authorization happen inside the transaction, but
            // before the advisory lock. The canonical row itself is fetched
            // again only after serialization, so no stale Open state is used.
            var identity = await _taxPeriodRepository.GetIdentityAsync(
                taxPeriodId,
                cancellationToken);
            if (identity is null)
            {
                throw new NotFoundException("Tax period not found.");
            }

            if (identity.OwnerId != userId)
            {
                throw new ForbiddenException(
                    "You do not have permission to access this business.");
            }

            await _transactionLock.AcquireOwnerYearLocksAsync(
                identity.OwnerId,
                [identity.Year],
                cancellationToken);

            var taxPeriod = await _taxPeriodRepository.GetCanonicalByIdAsync(
                identity.TaxPeriodId,
                cancellationToken);
            if (taxPeriod is null)
            {
                throw new NotFoundException("Tax period not found.");
            }

            if (taxPeriod.Status != TaxPeriodStatuses.Open)
            {
                throw new BadRequestException(
                    $"Tax period must be in Open status. Current status: {taxPeriod.Status}.");
            }

            var preview = await _taxPeriodRepository.GetPreviewAsync(
                taxPeriod.Id,
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

            if (preview.Warnings.Count > 0 && !request.ConfirmWarnings)
            {
                throw new ConflictException(
                    "Tax period contains warnings. Confirm warnings before closing.");
            }

            await EnsureS2eCanCloseAsync(
                identity.OwnerId,
                taxPeriod.PeriodStartDate,
                taxPeriod.PeriodEndDate,
                cancellationToken);

            taxPeriod.SalesRevenue = preview.SalesRevenue;
            taxPeriod.OtherRevenue = preview.OtherRevenue;
            taxPeriod.TotalRevenue = preview.TotalRevenue;
            taxPeriod.TaxableRevenue = preview.TaxableRevenue;
            taxPeriod.Status = TaxPeriodStatuses.Closed;
            taxPeriod.ClosedAt = DateTime.UtcNow;
            taxPeriod.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

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
        catch (Exception originalException)
        {
            try
            {
                // Request cancellation must not leave the database transaction
                // open. Rollback gets its own non-cancelled cleanup token.
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            }
            catch (Exception rollbackException)
            {
                // Preserve the business/cancellation failure as the observable
                // exception while retaining rollback diagnostics for logging.
                try
                {
                    originalException.Data["TaxPeriodCloseRollbackException"] =
                        rollbackException;
                }
                catch
                {
                    // Some custom exception Data dictionaries can be read-only.
                }
            }

            throw;
        }
    }

    private async Task EnsureS2eCanCloseAsync(
        Guid ownerId,
        DateTime fromInclusive,
        DateTime toExclusive,
        CancellationToken cancellationToken)
    {
        var businesses = await _taxPeriodRepository
            .GetBusinessesWithCategoriesByOwnerAsync(
                ownerId,
                cancellationToken);
        var blockerCount = 0;
        var firstMessage = string.Empty;

        foreach (var business in businesses)
        {
            var projection = await _s2eProjector.ProjectAsync(
                ownerId,
                business.Id,
                fromInclusive,
                toExclusive,
                cancellationToken);
            blockerCount += projection.Blockers.Count;
            if (firstMessage.Length == 0 && projection.Blockers.Count > 0)
            {
                firstMessage = projection.Blockers[0].Message;
            }
        }

        if (blockerCount > 0)
        {
            throw new ConflictException(
                $"S2e chưa sẵn sàng để khóa kỳ ({blockerCount} lỗi). {firstMessage}");
        }
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

        // TaxPeriod.BusinessId chỉ đóng vai trò anchor để xác định Owner.
        var anchorBusiness =
            await _taxPeriodRepository.GetBusinessWithCategoryAsync(
                taxPeriod.BusinessId,
                cancellationToken);

        if (anchorBusiness is null)
        {
            throw new NotFoundException("Business not found.");
        }

        var ownerBusinesses =
            await _taxPeriodRepository
                .GetBusinessesWithCategoriesByOwnerAsync(
                    anchorBusiness.OwnerId,
                    cancellationToken);

        if (ownerBusinesses.Count == 0)
        {
            throw new BadRequestException(
                "No active business profile exists for this owner.");
        }

        // Revenue được tách riêng theo từng BusinessProfile/location.
        var periodBusinessRevenues =
            new List<(BusinessProfile Business, decimal Revenue)>();

        foreach (var ownerBusiness in ownerBusinesses)
        {
            var revenue =
                await _taxPeriodRepository
                    .GetRevenueForBusinessInPeriodAsync(
                        ownerBusiness.Id,
                        taxPeriod.PeriodStartDate,
                        taxPeriod.PeriodEndDate,
                        cancellationToken);

            if (revenue <= 0m)
            {
                continue;
            }

            if (ownerBusiness.MainCategory is null)
            {
                throw new BadRequestException(
                    $"Business category is required for business '{ownerBusiness.BusinessName}'.");
            }

            periodBusinessRevenues.Add(
                (ownerBusiness, revenue));
        }

        if (periodBusinessRevenues.Count == 0)
        {
            throw new BadRequestException(
                "No completed sales revenue exists for this owner in the tax period.");
        }

        var totalPeriodRevenue =
            periodBusinessRevenues.Sum(x => x.Revenue);

        // Threshold/form là Owner-level.
        var annualRevenue =
            await _taxPeriodRepository
                .GetAnnualRevenueByOwnerAsync(
                    anchorBusiness.OwnerId,
                    taxPeriod.Year,
                    cancellationToken);

        var policyDate = DateOnly.FromDateTime(
            taxPeriod.PeriodEndDate.AddDays(-1));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (policyDate > today)
        {
            policyDate = today;
        }

        var taxPolicy = await _taxPolicyService.GetEffectiveAsync(
            policyDate,
            cancellationToken);
        var annualRevenueThreshold = taxPolicy.AnnualRevenueThreshold;

        var isTaxableBusiness =
            annualRevenue >
            annualRevenueThreshold;

        var recommendedFormCode =
            isTaxableBusiness
                ? "01/CNKD"
                : "01/TKN-CNKD";

        // Mức trừ TNCN theo chính sách là một pool chung của Owner.
        decimal remainingDeduction;

        if (isTaxableBusiness)
        {
            var previousAnnualRevenue =
                await _taxPeriodRepository
                    .GetAnnualRevenueBeforePeriodByOwnerAsync(
                        anchorBusiness.OwnerId,
                        taxPeriod.Year,
                        taxPeriod.PeriodStartDate,
                        cancellationToken);

            var alreadyConsumedDeduction =
                Math.Min(
                    previousAnnualRevenue,
                    annualRevenueThreshold);

            remainingDeduction =
                Math.Max(
                    0m,
                    annualRevenueThreshold -
                    alreadyConsumedDeduction);
        }
        else
        {
            remainingDeduction =
                Math.Max(
                    0m,
                    annualRevenueThreshold -
                    annualRevenue);
        }

        // Phân bổ deduction cho PIT rate cao trước (phương án có lợi hơn).
        var pitDeductionByBusiness =
            new Dictionary<Guid, decimal>();

        if (isTaxableBusiness)
        {
            var deductionToAllocate =
                remainingDeduction;

            foreach (var item in periodBusinessRevenues
                         .OrderByDescending(
                             x => x.Business.MainCategory!.PitRate)
                         .ThenBy(
                             x => x.Business.Id == taxPeriod.BusinessId
                                 ? 0
                                 : 1)
                         .ThenBy(x => x.Business.CreatedAt))
            {
                var allocated =
                    Math.Min(
                        item.Revenue,
                        deductionToAllocate);

                pitDeductionByBusiness[item.Business.Id] =
                    allocated;

                deductionToAllocate =
                    Math.Max(
                        0m,
                        deductionToAllocate - allocated);
            }

            remainingDeduction =
                deductionToAllocate;
        }

        await _taxPeriodRepository
            .SetPreviousCalculationsAsSupersededAsync(
                taxPeriod.Id,
                cancellationToken);

        var version =
            await _taxPeriodRepository.GetNextCalculationVersionAsync(
                taxPeriod.Id,
                cancellationToken);

        var now = DateTime.UtcNow;

        var calculation = new TaxCalculation
        {
            Id = Guid.NewGuid(),
            TaxPeriodId = taxPeriod.Id,
            Version = version,
            Status = TaxCalculationStatuses.Completed,

            TotalRevenue = totalPeriodRevenue,
            TotalTaxableRevenue = totalPeriodRevenue,

            TotalVatTaxAmount = 0m,
            TotalPersonalIncomeTaxAmount = 0m,
            TotalTaxBeforeExemption = 0m,
            TotalExemptionAmount = 0m,
            TotalTaxPayableAmount = 0m,

            CalculatedAt = now,
            CalculatedByUserId = userId,

            AnnualRevenueAtCalculation =
                annualRevenue,

            ApplicableRevenueThreshold =
                annualRevenueThreshold,

            RecommendedFormCode =
                recommendedFormCode,

            RemainingPitDeduction =
                remainingDeduction,

            IsCurrent = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Anchor business đứng trước để giữ thứ tự hiển thị ổn định.
        var displayItems =
            periodBusinessRevenues
                .OrderBy(
                    x => x.Business.Id == taxPeriod.BusinessId
                        ? 0
                        : 1)
                .ThenBy(x => x.Business.CreatedAt)
                .ThenBy(x => x.Business.BusinessName)
                .ToList();

        var displayOrder = 1;

        foreach (var item in displayItems)
        {
            var profile =
                item.Business;

            var category =
                profile.MainCategory!;

            var revenue =
                item.Revenue;

            var vatTaxAmount =
                isTaxableBusiness
                    ? decimal.Round(
                        revenue *
                        category.VatRate /
                        100m,
                        2,
                        MidpointRounding.AwayFromZero)
                    : 0m;

            var pitDeductibleRevenue =
                isTaxableBusiness &&
                pitDeductionByBusiness.TryGetValue(
                    profile.Id,
                    out var allocatedDeduction)
                    ? allocatedDeduction
                    : 0m;

            var pitTaxableRevenue =
                isTaxableBusiness
                    ? revenue
                    : 0m;

            var pitRevenue =
                isTaxableBusiness
                    ? Math.Max(
                        0m,
                        revenue - pitDeductibleRevenue)
                    : 0m;

            var pitTaxAmount =
                isTaxableBusiness
                    ? decimal.Round(
                        pitRevenue *
                        category.PitRate /
                        100m,
                        2,
                        MidpointRounding.AwayFromZero)
                    : 0m;

            calculation.Lines.Add(
                new TaxCalculationLine
                {
                    Id = Guid.NewGuid(),
                    TaxCalculationId =
                        calculation.Id,

                    BusinessLocationId =
                        profile.Id,

                    BusinessLocationCode =
                        profile.BusinessLocationCode,

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
                        revenue,

                    VatTaxableRevenue =
                        revenue,

                    VatNonTaxableRevenue =
                        0m,

                    ZeroRatedVatRevenue =
                        0m,

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

                    DisplayOrder =
                        displayOrder++,

                    CreatedAt = now,
                    UpdatedAt = now
                });
        }

        // Tổng calculation luôn SUM từ các location lines.
        calculation.TotalVatTaxAmount =
            calculation.Lines.Sum(x => x.VatTaxAmount);

        calculation.TotalPersonalIncomeTaxAmount =
            calculation.Lines.Sum(
                x => x.PersonalIncomeTaxAmount);

        calculation.TotalTaxBeforeExemption =
            calculation.TotalVatTaxAmount +
            calculation.TotalPersonalIncomeTaxAmount;

        calculation.TotalTaxPayableAmount =
            calculation.TotalTaxBeforeExemption -
            calculation.TotalExemptionAmount;

        // TaxPeriod vẫn giữ BusinessId anchor, nhưng amount thuế là Owner-level.
        taxPeriod.VatTaxAmount =
            calculation.TotalVatTaxAmount;

        taxPeriod.PersonalIncomeTaxAmount =
            calculation.TotalPersonalIncomeTaxAmount;

        taxPeriod.EstimatedTax =
            calculation.TotalTaxPayableAmount;

        taxPeriod.TaxAmountDebt =
            calculation.TotalTaxPayableAmount;

        taxPeriod.Status =
            TaxPeriodStatuses.Calculated;

        taxPeriod.CalculatedAt =
            now;

        taxPeriod.UpdatedAt =
            now;

        await _taxCalculationRepository.AddAsync(
            calculation);

        await _taxPeriodRepository.SaveChangesAsync(
            cancellationToken);

        return new TaxCalculationResponse
        {
            TaxPeriodId =
                taxPeriod.Id,

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

            AnnualRevenueAtCalculation =
                calculation.AnnualRevenueAtCalculation,

            ApplicableRevenueThreshold =
                calculation.ApplicableRevenueThreshold,

            RecommendedFormCode =
                calculation.RecommendedFormCode,

            RemainingPitDeduction =
                calculation.RemainingPitDeduction,

            Status =
                taxPeriod.Status,

            CalculatedAt =
                calculation.CalculatedAt,

            Lines =
                calculation.Lines
                    .OrderBy(x => x.DisplayOrder)
                    .Select(line =>
                        new TaxCalculationLineResponse
                        {
                            Id =
                                line.Id,

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
                        })
                    .ToList()
        };
    }
}
