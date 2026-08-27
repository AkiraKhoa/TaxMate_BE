using TaxMate.Model.Common;
using TaxMate.Model.DTO.TaxPeriod;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;
using System.Text.Json;
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
    private readonly IInventoryMovementRepository _inventoryMovements;
    private readonly IInventoryQuarterFinalizer _inventoryValuation;
    private readonly IOwnerRevenueProjector? _ownerRevenue;
    private readonly IGenericRepository<RevenueThresholdAlert>? _thresholdAlerts;

    public TaxPeriodService(
        ITaxPeriodRepository taxPeriodRepository,
        ITaxCalculationRepository taxCalculationRepository,
        ITaxPolicyService taxPolicyService,
        IUnitOfWork unitOfWork,
        IAccountingTransactionLockRepository transactionLock,
        IS2eBookProjector s2eProjector,
        IInventoryMovementRepository inventoryMovements,
        IInventoryQuarterFinalizer inventoryValuation,
        IOwnerRevenueProjector? ownerRevenue = null,
        IGenericRepository<RevenueThresholdAlert>? thresholdAlerts = null)
    {
        _taxPeriodRepository = taxPeriodRepository;
        _taxCalculationRepository = taxCalculationRepository;
        _taxPolicyService = taxPolicyService;
        _unitOfWork = unitOfWork;
        _transactionLock = transactionLock;
        _s2eProjector = s2eProjector;
        _inventoryMovements = inventoryMovements;
        _inventoryValuation = inventoryValuation;
        _ownerRevenue = ownerRevenue;
        _thresholdAlerts = thresholdAlerts;
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
        RejectTknPeriod(taxPeriod);

        var preview = await _taxPeriodRepository.GetPreviewAsync(
            taxPeriodId,
            cancellationToken);

        if (preview is null)
        {
            throw new NotFoundException("Tax period not found.");
        }

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

            RejectTknPeriod(taxPeriod);

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

            if (taxPeriod.PeriodType == TaxPeriodTypes.Quarterly &&
                taxPeriod.Quarter.HasValue)
            {
                var businesses = await _taxPeriodRepository
                    .GetBusinessesWithCategoriesByOwnerAsync(
                        identity.OwnerId,
                        cancellationToken);
                foreach (var business in businesses.OrderBy(x => x.Id))
                {
                    var movements = await _inventoryMovements
                        .GetBeforeForUpdateAsync(
                            business.Id,
                            taxPeriod.PeriodEndDate,
                            cancellationToken);
                    var valuation = _inventoryValuation.StageFinalizeBookPeriod(
                        movements,
                        taxPeriod.PeriodStartDate,
                        taxPeriod.PeriodEndDate);
                    if (!valuation.CanFinalize)
                    {
                        var blocker = valuation.Blockers[0];
                        throw new UnprocessableEntityException(
                            blocker.Code,
                            blocker.Message);
                    }
                }
            }

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

        RejectTknPeriod(taxPeriod);

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

        if (_ownerRevenue is null)
            throw new InvalidOperationException(
                "Owner revenue projector is not configured.");

        var owner = anchorBusiness.Owner;
        if (!owner.TaxProfileConfirmedAt.HasValue ||
            owner.PersonalIncomeTaxMethod is null ||
            !owner.TaxMethodEffectiveYear.HasValue ||
            owner.TaxMethodEffectiveYear.Value > taxPeriod.Year)
        {
            throw new ConflictException(
                "Hãy xác nhận nhóm doanh thu và phương pháp TNCN trước khi tính 01/CNKD.");
        }
        if (_thresholdAlerts is not null &&
            (await _thresholdAlerts.FindAsync(x =>
                x.OwnerId == owner.Id &&
                x.ThresholdCode == RevenueThresholdCodes.Crossed3B &&
                x.Status == RevenueThresholdAlertStatuses.Acknowledged &&
                x.Year < taxPeriod.Year)).Any())
        {
            throw new ConflictException(
                "Phải xác nhận chuyển sang IncomeBased trước khi tính kỳ đầu năm mới.");
        }

        var annualProjection = await _ownerRevenue.ProjectCalendarYearAsync(
            anchorBusiness.OwnerId,
            taxPeriod.BusinessId,
            taxPeriod.Year,
            cancellationToken);
        ThrowRevenueBlockers(annualProjection.Blockers);
        var annualRevenue = annualProjection.TotalRevenue;

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
        if (annualRevenue > taxPolicy.SupportedRevenueCeiling)
            throw new ConflictException(
                "Doanh thu năm đã vượt 50 tỷ đồng, ngoài phạm vi lập hồ sơ của TaxMate.");
        if (annualRevenue <= annualRevenueThreshold)
            throw new ConflictException(
                "Doanh thu năm chưa vượt 1 tỷ đồng; hãy dùng 01/TKN-CNKD.");

        var taxMethod = owner.PersonalIncomeTaxMethod;
        var firstCrossingQuarter = ResolveFirstCrossingQuarter(
            annualProjection, annualRevenueThreshold);
        if (taxPeriod.Quarter.HasValue &&
            owner.TaxMethodEffectiveYear.Value == taxPeriod.Year &&
            firstCrossingQuarter.HasValue &&
            taxPeriod.Quarter.Value < firstCrossingQuarter.Value)
        {
            throw new ConflictException(
                $"01/CNKD bắt đầu từ quý {firstCrossingQuarter}; các quý trước đó không áp dụng.");
        }
        var calculationStart = ResolveCalculationWindowStart(
            taxPeriod,
            annualProjection,
            annualRevenueThreshold,
            owner.TaxMethodEffectiveYear.Value);
        var periodProjection = await _ownerRevenue.ProjectAsync(
            anchorBusiness.OwnerId,
            taxPeriod.BusinessId,
            calculationStart,
            taxPeriod.PeriodEndDate,
            cancellationToken);
        ThrowRevenueBlockers(periodProjection.Blockers);
        if (periodProjection.TotalRevenue <= 0m)
            throw new BadRequestException(
                "Không có doanh thu kinh doanh trong cửa sổ tính thuế.");

        var previousRevenue = calculationStart <= annualProjection.StartNaiveUtc
            ? 0m
            : (await _ownerRevenue.ProjectAsync(
                anchorBusiness.OwnerId,
                taxPeriod.BusinessId,
                annualProjection.StartNaiveUtc,
                calculationStart,
                cancellationToken)).TotalRevenue;
        var remainingDeduction = taxMethod ==
                PersonalIncomeTaxMethods.RevenueBased
            ? Math.Max(0m, annualRevenueThreshold - previousRevenue)
            : 0m;

        // Phân bổ deduction cho PIT rate cao trước (phương án có lợi hơn).
        var pitDeductionByBusiness =
            new Dictionary<Guid, decimal>();

        if (taxMethod == PersonalIncomeTaxMethods.RevenueBased)
        {
            var deductionToAllocate =
                remainingDeduction;

            var pitRates = ownerBusinesses
                .Where(x => x.MainCategory is not null)
                .Select(x => x.MainCategory!)
                .GroupBy(x => x.BusinessCategoryId)
                .ToDictionary(x => x.Key, x => x.First().PitRate);
            foreach (var item in periodProjection.Groups
                         .OrderByDescending(x => pitRates.GetValueOrDefault(
                             x.BusinessCategoryId)))
            {
                var allocated =
                    Math.Min(
                        item.TotalRevenue,
                        deductionToAllocate);

                pitDeductionByBusiness[item.BusinessCategoryId] =
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
            TaxMethod = taxMethod,
            TaxMethodEffectiveYear = owner.TaxMethodEffectiveYear,
            CalculationRuleVersion = "TT152-2025-01CNKD-v1",

            TotalRevenue = periodProjection.TotalRevenue,
            TotalTaxableRevenue = periodProjection.TotalRevenue,

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

            RecommendedFormCode = TaxFormCodes.Form01Cnkd,

            RemainingPitDeduction =
                remainingDeduction,

            CalculationDataJson = JsonSerializer.Serialize(new
            {
                taxMethod,
                taxMethodEffectiveYear = owner.TaxMethodEffectiveYear,
                revenueBracket = owner.DeclaredRevenueBracket,
                annualRevenue,
                threshold = annualRevenueThreshold,
                windowFromInclusive = calculationStart,
                windowToExclusive = taxPeriod.PeriodEndDate,
                isFirstCrossingWindow = calculationStart < taxPeriod.PeriodStartDate,
                ruleVersion = "TT152-2025-01CNKD-v1"
            }),

            IsCurrent = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var categoryById = ownerBusinesses
            .Where(x => x.MainCategory is not null)
            .Select(x => x.MainCategory!)
            .GroupBy(x => x.BusinessCategoryId)
            .ToDictionary(x => x.Key, x => x.First());
        var displayItems = periodProjection.Groups
            .OrderBy(x => x.BusinessCategoryCode)
            .ToList();

        var displayOrder = 1;

        foreach (var item in displayItems)
        {
            if (!categoryById.TryGetValue(
                    item.BusinessCategoryId, out var category))
                throw new ConflictException(
                    $"Không tìm thấy ngành nghề {item.BusinessCategoryCode}.");

            var revenue = item.TotalRevenue;

            var vatTaxAmount =
                decimal.Round(revenue * item.VatRate / 100m, 2,
                    MidpointRounding.AwayFromZero);

            var pitDeductibleRevenue =
                taxMethod == PersonalIncomeTaxMethods.RevenueBased &&
                pitDeductionByBusiness.TryGetValue(
                    item.BusinessCategoryId,
                    out var allocatedDeduction)
                    ? allocatedDeduction
                    : 0m;

            var pitTaxableRevenue =
                revenue;

            var pitRevenue =
                Math.Max(0m, revenue - pitDeductibleRevenue);

            var pitTaxAmount =
                decimal.Round(pitRevenue * category.PitRate / 100m, 2,
                    MidpointRounding.AwayFromZero);

            calculation.Lines.Add(
                new TaxCalculationLine
                {
                    Id = Guid.NewGuid(),
                    TaxCalculationId =
                        calculation.Id,

                    BusinessLocationId = null,

                    BusinessLocationCode = null,

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
                        item.VatRate,

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

            TaxMethod = calculation.TaxMethod,

            TaxMethodEffectiveYear = calculation.TaxMethodEffectiveYear,

            CalculationRuleVersion = calculation.CalculationRuleVersion,

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

    private static DateTime ResolveCalculationWindowStart(
        TaxPeriod period,
        OwnerRevenueProjection annual,
        decimal threshold,
        int methodEffectiveYear)
    {
        if (!period.Quarter.HasValue || methodEffectiveYear != period.Year)
            return period.PeriodStartDate;

        var crossingQuarter = ResolveFirstCrossingQuarter(annual, threshold);
        if (!crossingQuarter.HasValue)
            return period.PeriodStartDate;
        return crossingQuarter.Value == period.Quarter.Value
            ? annual.StartNaiveUtc
            : period.PeriodStartDate;
    }

    private static int? ResolveFirstCrossingQuarter(
        OwnerRevenueProjection annual,
        decimal threshold)
    {
        decimal cumulative = 0m;
        var crossing = annual.Lines
            .OrderBy(x => x.DocumentDate)
            .ThenBy(x => x.SourceType)
            .ThenBy(x => x.SourceId)
            .FirstOrDefault(x =>
            {
                cumulative += x.Amount;
                return cumulative > threshold;
            });
        return crossing is null
            ? null
            : ((crossing.DocumentDate.AddHours(7).Month - 1) / 3) + 1;
    }

    private static void ThrowRevenueBlockers(
        IReadOnlyCollection<OwnerRevenueBlocker> blockers)
    {
        if (blockers.Count == 0)
            return;
        throw new ConflictException(
            $"Dữ liệu doanh thu chưa sẵn sàng ({blockers.Count} lỗi). " +
            blockers.First().Message);
    }

    private static void RejectTknPeriod(TaxPeriod taxPeriod)
    {
        if (taxPeriod.PeriodType == TaxPeriodTypes.Tkn)
        {
            throw new BadRequestException(
                "Use the dedicated TKN workflow for this tax period.");
        }
    }
}
