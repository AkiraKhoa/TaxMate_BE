using System.Text.Json;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.Tax;
using TaxMate.Model.DTO.TaxPeriod;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using BadRequestException = TaxMate.Service.Exceptions.BadRequestException;

namespace TaxMate.Service.Services;

public sealed class TknTaxPeriodService : ITknTaxPeriodService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> SupportedIndicator08Categories = new(
        [
            "DIST_GOODS", "PROD_TRANSPORT", "SERVICE_CONSTRUCT",
            "FNB", "SERVICE", "OTHER"
        ],
        StringComparer.OrdinalIgnoreCase);
    private readonly ITaxPeriodRepository _periods;
    private readonly ITaxCalculationRepository _calculations;
    private readonly ITaxPolicyService _policies;
    private readonly IOwnerRevenueProjector _ownerRevenue;
    private readonly IAnnualTaxAggregateService _annualAggregate;
    private readonly IQttCalculationService _qttCalculations;
    private readonly IQttDeclarationService _qttDeclarations;
    private readonly ITaxDeclarationRepository _declarations;
    private readonly TimeProvider _timeProvider;

    public TknTaxPeriodService(ITaxPeriodRepository periods,
        ITaxCalculationRepository calculations, ITaxPolicyService policies,
        IOwnerRevenueProjector ownerRevenue,
        IAnnualTaxAggregateService annualAggregate,
        IQttCalculationService qttCalculations,
        IQttDeclarationService qttDeclarations,
        ITaxDeclarationRepository declarations,
        TimeProvider? timeProvider = null)
    {
        _periods = periods;
        _calculations = calculations;
        _policies = policies;
        _ownerRevenue = ownerRevenue;
        _annualAggregate = annualAggregate;
        _qttCalculations = qttCalculations;
        _qttDeclarations = qttDeclarations;
        _declarations = declarations;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<TknTaxPeriodPreviewResponse> GetPreviewAsync(Guid userId,
        Guid taxPeriodId, CancellationToken cancellationToken = default)
    {
        var context = await LoadAsync(userId, taxPeriodId, cancellationToken);
        var projection = await GetRevenueProjectionAsync(
            userId, context.Period, cancellationToken);
        var eligibilityProjection = context.Period.FilingWindow == TknFilingWindows.FirstHalf
            ? projection
            : await _ownerRevenue.ProjectCalendarYearAsync(
                userId, context.Period.BusinessId, context.Period.Year, cancellationToken);
        var policy = await GetPolicyAsync(context.Period, cancellationToken);
        var blockers = eligibilityProjection.Blockers
            .Select(x => x.Message)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (eligibilityProjection.TotalRevenue > policy.AnnualRevenueThreshold)
            blockers.Add("Doanh thu đã vượt ngưỡng áp dụng 01/TKN-CNKD; hãy chuyển sang luồng kê khai theo quý.");
        blockers.AddRange(GetUnsupportedCategoryBlockers(projection.Groups));
        if (!HasRevenueWindowEnded(context.Period))
            blockers.Add("Chưa thể chốt TKN trước khi kỳ doanh thu kết thúc.");
        blockers = blockers.Distinct(StringComparer.Ordinal).ToList();
        var warnings = blockers.ToList();
        if (projection.TotalRevenue <= 0m)
            warnings.Add("Chưa có doanh thu kinh doanh trong kỳ thông báo; hãy xác nhận số 0 trước khi chốt.");
        return new TknTaxPeriodPreviewResponse(context.Period.Id, context.Period.Year,
            context.Period.PeriodStartDate, context.Period.PeriodEndDate,
            context.Period.DueDate, projection.TotalRevenue,
            projection.Lines.Select(x => x.BusinessCategoryId).Distinct().Count(),
            blockers.Count == 0, warnings);
    }

    public async Task<CloseTknTaxPeriodResponse> CloseAsync(Guid userId,
        Guid taxPeriodId, CloseTknTaxPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadAsync(userId, taxPeriodId, cancellationToken);
        if (context.Period.Status != TaxPeriodStatuses.Open)
            throw new BadRequestException($"TKN period must be Open. Current status: {context.Period.Status}.");
        var preview = await GetPreviewAsync(userId, taxPeriodId, cancellationToken);
        if (!preview.CanClose)
            throw new BadRequestException("TKN period has source-data blockers and cannot be closed.");
        if (preview.Warnings.Count > 0 && !request.ConfirmWarnings)
            throw new ConflictException("Confirm TKN period warnings before closing.");
        var now = UtcNow();
        context.Period.SalesRevenue = preview.TotalRevenue;
        context.Period.OtherRevenue = 0m;
        context.Period.TotalRevenue = preview.TotalRevenue;
        context.Period.TaxableRevenue = 0m;
        context.Period.Status = TaxPeriodStatuses.Closed;
        context.Period.ClosedAt = now;
        context.Period.UpdatedAt = now;
        await _periods.SaveChangesAsync(cancellationToken);
        return new CloseTknTaxPeriodResponse(context.Period.Id,
            context.Period.Status, preview.TotalRevenue, now);
    }

    public async Task<TknTaxCalculationResponse> CalculateAsync(Guid userId,
        Guid taxPeriodId, CancellationToken cancellationToken = default)
    {
        var context = await LoadAsync(userId, taxPeriodId, cancellationToken);
        if (context.Period.Status != TaxPeriodStatuses.Closed)
            throw new BadRequestException($"TKN period must be Closed. Current status: {context.Period.Status}.");
        EnsureRevenueWindowEnded(context.Period);
        var projection = await GetRevenueProjectionAsync(
            userId, context.Period, cancellationToken);
        if (projection.Blockers.Count > 0)
            throw new BadRequestException(projection.Blockers[0].Message);
        var annualProjection = await _ownerRevenue.ProjectCalendarYearAsync(
            userId, context.Period.BusinessId, context.Period.Year, cancellationToken);
        var isFirstHalf = context.Period.FilingWindow == TknFilingWindows.FirstHalf;
        if (!isFirstHalf && annualProjection.Blockers.Count > 0)
            throw new BadRequestException(annualProjection.Blockers[0].Message);
        var annualRevenue = annualProjection.TotalRevenue;
        var policy = await GetPolicyAsync(context.Period, cancellationToken);
        var eligibilityRevenue = isFirstHalf
            ? projection.TotalRevenue
            : annualRevenue;
        if (eligibilityRevenue > policy.AnnualRevenueThreshold)
            throw new BadRequestException("Owner revenue exceeds the TKN threshold; use the quarterly declaration workflow.");
        EnsureSupportedCategories(projection.Groups);

        await _periods.SetPreviousCalculationsAsSupersededAsync(context.Period.Id, cancellationToken);
        var version = await _periods.GetNextCalculationVersionAsync(context.Period.Id, cancellationToken);
        var now = UtcNow();
        var total = projection.TotalRevenue;
        var calculation = new TaxCalculation
        {
            Id = Guid.NewGuid(), TaxPeriodId = context.Period.Id, Version = version,
            TaxMethod = PersonalIncomeTaxMethods.NotApplicable,
            TaxMethodEffectiveYear = null, Status = TaxCalculationStatuses.Completed,
            CalculationRuleVersion = $"VN-TKN-{policy.AnnualRevenueThresholdEffectiveFrom:yyyyMMdd}",
            TotalRevenue = total, TotalTaxableRevenue = 0m,
            TotalVatTaxAmount = 0m, TotalPersonalIncomeTaxAmount = 0m,
            TotalTaxBeforeExemption = 0m, TotalExemptionAmount = 0m,
            TotalTaxPayableAmount = 0m, AnnualRevenueAtCalculation = annualRevenue,
            ApplicableRevenueThreshold = policy.AnnualRevenueThreshold,
            RecommendedFormCode = TaxFormCodes.Form01TknCnkd,
            RemainingPitDeduction = 0m, CalculatedAt = now,
            CalculatedByUserId = userId, IsCurrent = true,
            CreatedAt = now, UpdatedAt = now,
            CalculationDataJson = JsonSerializer.Serialize(new
            {
                schemaVersion = 1, taxMethod = (string?)null,
                taxMethodCode = PersonalIncomeTaxMethods.NotApplicable,
                formCode = TaxFormCodes.Form01TknCnkd,
                revenueWindowStart = context.Period.PeriodStartDate,
                revenueWindowEnd = context.Period.PeriodEndDate,
                annualRevenue, applicableThreshold = policy.AnnualRevenueThreshold,
                calculationRuleVersion = $"VN-TKN-{policy.AnnualRevenueThresholdEffectiveFrom:yyyyMMdd}"
            }, JsonOptions)
        };
        var order = 1;
        foreach (var group in projection.Groups
                     .OrderBy(x => x.BusinessCategoryCode))
        {
            calculation.Lines.Add(new TaxCalculationLine
            {
                Id = Guid.NewGuid(), TaxCalculationId = calculation.Id,
                BusinessCategoryId = group.BusinessCategoryId,
                SectionCode = "I",
                IndicatorCode = "08",
                BusinessActivityCode = group.BusinessCategoryCode,
                BusinessActivityName = group.BusinessCategoryName,
                TotalRevenue = group.TotalRevenue, VatTaxableRevenue = 0m,
                VatNonTaxableRevenue = group.TotalRevenue, ZeroRatedVatRevenue = 0m,
                VatTaxRate = 0m, VatTaxAmount = 0m,
                PersonalIncomeTaxableRevenue = 0m,
                PersonalIncomeTaxDeductibleRevenue = 0m,
                PersonalIncomeTaxRevenue = 0m, PersonalIncomeTaxRate = 0m,
                PersonalIncomeTaxAmount = 0m, DisplayOrder = order++,
                CreatedAt = now, UpdatedAt = now
            });
        }
        context.Period.VatTaxAmount = 0m;
        context.Period.PersonalIncomeTaxAmount = 0m;
        context.Period.EstimatedTax = 0m;
        context.Period.TaxAmountDebt = 0m;
        context.Period.Status = TaxPeriodStatuses.Calculated;
        context.Period.CalculatedAt = now;
        context.Period.UpdatedAt = now;
        await _calculations.AddAsync(calculation);
        await _periods.SaveChangesAsync(cancellationToken);
        return new TknTaxCalculationResponse(context.Period.Id, calculation.Id,
            version, total, policy.AnnualRevenueThreshold,
            TaxFormCodes.Form01TknCnkd, now);
    }

    public async Task<TknQttNextStepResponse> GetQttNextStepAsync(
        Guid userId,
        Guid taxPeriodId,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadAsync(userId, taxPeriodId, cancellationToken);
        EnsureYearEndBridgePeriod(context.Period);

        var preview = await _annualAggregate.PreviewAsync(
            userId,
            context.Period.BusinessId,
            context.Period.Year,
            cancellationToken);
        var incomeBasedPitPaid = preview.PitPayments.Payments
            .Where(x =>
                x.IncludedInIndicator15 &&
                x.SourceTaxMethod == PersonalIncomeTaxMethods.IncomeBased)
            .Sum(x => x.Amount);
        var requiresSourceReview = preview.PitPayments.Payments.Any(x =>
            x.IncludedInIndicator15 &&
            string.IsNullOrWhiteSpace(x.SourceTaxMethod));
        var existing = await GetExistingQttDeclarationAsync(
            context.Period,
            cancellationToken);
        var canEditExisting = existing is null ||
            existing.Status == TaxDeclarationStatuses.Draft;
        var canCreate =
            preview.Eligibility == QttEligibility.UnderOneBillionRefund &&
            incomeBasedPitPaid > 0m &&
            preview.CanClose &&
            canEditExisting;
        var hasBridgeChoice =
            preview.Eligibility == QttEligibility.UnderOneBillionRefund &&
            incomeBasedPitPaid > 0m &&
            canEditExisting;

        return BuildBridgeResponse(
            context.Period,
            preview,
            incomeBasedPitPaid,
            requiresSourceReview,
            hasBridgeChoice,
            canCreate,
            context.Period.TknQttBridgeChoice,
            context.Period.TknQttBridgeChoiceAt,
            existing);
    }

    public async Task<TknQttNextStepResponse> ApplyQttNextStepAsync(
        Guid userId,
        Guid taxPeriodId,
        ApplyTknQttNextStepRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var choice = request.Choice?.Trim();
        if (string.IsNullOrWhiteSpace(choice) ||
            !TknQttBridgeChoices.All.Contains(choice, StringComparer.Ordinal))
        {
            throw new BadRequestException(
                "Choice must be Later, Refund, or Offset.");
        }

        var current = await GetQttNextStepAsync(
            userId,
            taxPeriodId,
            cancellationToken);
        if (!current.Choices.Contains(choice, StringComparer.Ordinal))
        {
            throw new ConflictException(
                "TKN này không có bước xử lý QTT cho lựa chọn đã gửi.");
        }

        if (choice == TknQttBridgeChoices.Later)
        {
            var laterContext = await LoadAsync(userId, taxPeriodId, cancellationToken);
            await PersistBridgeChoiceAsync(
                laterContext.Period,
                choice,
                cancellationToken);
            return await GetQttNextStepAsync(
                userId,
                taxPeriodId,
                cancellationToken);
        }

        if (!current.CanCreateQttDraft)
        {
            throw new ConflictException(
                current.RequiresPaymentSourceReview
                    ? "Khoản PIT đã nộp chưa truy được snapshot phương pháp nguồn; hãy rà soát trước khi tạo QTT."
                    : "TKN này không đủ điều kiện tạo QTT xử lý PIT IncomeBased nộp thừa.");
        }

        if (choice == TknQttBridgeChoices.Refund &&
            !request.RefundPaymentAccountId.HasValue)
        {
            throw new BadRequestException(
                "RefundPaymentAccountId is required for a refund choice.");
        }

        if (choice == TknQttBridgeChoices.Offset &&
            request.OffsetItems.Sum(x => x.OffsetAmount) !=
            current.IncomeBasedPitPaid)
        {
            throw new BadRequestException(
                "OffsetItems must allocate the full IncomeBased PIT payment for the Offset choice.");
        }

        var context = await LoadAsync(userId, taxPeriodId, cancellationToken);
        await _qttCalculations.CalculateAsync(
            userId,
            context.Period.BusinessId,
            context.Period.Year,
            cancellationToken);
        var declaration = await _qttDeclarations.CreateAsync(
            userId,
            context.Period.BusinessId,
            context.Period.Year,
            cancellationToken);

        var overpaid = declaration.Indicators.Indicator20;
        if (overpaid <= 0m)
        {
            throw new ConflictException(
                "QTT hiện hành không có số PIT nộp thừa để xử lý.");
        }

        if (choice == TknQttBridgeChoices.Refund)
        {
            var refundPaymentAccountId = request.RefundPaymentAccountId
                ?? throw new InvalidOperationException(
                    "RefundPaymentAccountId was validated before processing the refund choice.");

            if (declaration.Indicators.Indicator22 != overpaid ||
                declaration.Indicators.Indicator23 != 0m ||
                declaration.RefundAccount?.PaymentAccountId !=
                refundPaymentAccountId)
            {
                declaration = await _qttDeclarations.UpdateAllocationAsync(
                    userId,
                    context.Period.BusinessId,
                    declaration.DeclarationId,
                    new UpdateQttOverpaymentAllocationRequest
                    {
                        RefundAmount = overpaid,
                        OffsetAmount = 0m,
                        RefundPaymentAccountId = request.RefundPaymentAccountId,
                        ExpectedRevision = declaration.DraftRevision
                    },
                    cancellationToken);
            }
        }
        else
        {
            var offsetAmount = request.OffsetItems.Sum(x => x.OffsetAmount);
            if (offsetAmount != overpaid)
            {
                throw new BadRequestException(
                    "OffsetItems must allocate the full PIT overpayment for the Offset choice.");
            }

            if (!HasSameOffsetAllocation(declaration, request.OffsetItems, overpaid))
            {
                declaration = await _qttDeclarations.UpdateAllocationAsync(
                    userId,
                    context.Period.BusinessId,
                    declaration.DeclarationId,
                    new UpdateQttOverpaymentAllocationRequest
                    {
                        RefundAmount = 0m,
                        OffsetAmount = overpaid,
                        OffsetItems = request.OffsetItems,
                        ExpectedRevision = declaration.DraftRevision
                    },
                    cancellationToken);
            }
        }

        await PersistBridgeChoiceAsync(
            context.Period,
            choice,
            cancellationToken);
        var refreshed = await GetQttNextStepAsync(
            userId,
            taxPeriodId,
            cancellationToken);
        return WithSelectedChoice(refreshed, choice, declaration);
    }

    private async Task<ExistingQttState?> GetExistingQttDeclarationAsync(
        TaxPeriod tknPeriod,
        CancellationToken cancellationToken)
    {
        var annualPeriod = await _periods.GetYearAsync(
            tknPeriod.BusinessId,
            tknPeriod.Year,
            cancellationToken);
        if (annualPeriod is null)
        {
            return null;
        }

        var declaration = await _declarations.GetCurrentByTaxPeriodAndFormAsync(
            annualPeriod.Id,
            TaxFormCodes.Form02CnkdTncnQtt,
            cancellationToken);
        if (declaration is null)
        {
            return null;
        }

        var revision = 1;
        if (!string.IsNullOrWhiteSpace(declaration.FormDataJson))
        {
            var snapshot = JsonSerializer.Deserialize<QttFormSnapshot>(
                declaration.FormDataJson,
                JsonOptions);
            revision = snapshot?.DraftRevision ?? revision;
        }

        return new ExistingQttState(
            annualPeriod.Id,
            declaration.Id,
            declaration.Status,
            revision);
    }

    private static TknQttNextStepResponse BuildBridgeResponse(
        TaxPeriod period,
        QttPreviewResponse preview,
        decimal incomeBasedPitPaid,
        bool requiresSourceReview,
        bool hasBridgeChoice,
        bool canCreate,
        string? selectedChoice,
        DateTime? selectedChoiceAt,
        ExistingQttState? existing) => new()
    {
        TknTaxPeriodId = period.Id,
        TaxYear = period.Year,
        AnnualRevenue = preview.Revenue.Indicator09,
        IncomeBasedPitPaid = incomeBasedPitPaid,
        Eligibility = preview.Eligibility,
        RequiresPaymentSourceReview = requiresSourceReview,
        CanCreateQttDraft = canCreate,
        Choices = hasBridgeChoice ? TknQttBridgeChoices.All.ToList() : [],
        BlockingIssues = preview.HardBlockers
            .Concat(preview.Warnings.Where(x =>
                x.Code == "EvidenceReviewRequired"))
            .GroupBy(x => new { x.Code, x.SourceId, x.BusinessId })
            .Select(x => x.First())
            .ToList(),
        SelectedChoice = selectedChoice,
        SelectedChoiceAt = selectedChoiceAt,
        QttTaxPeriodId = existing?.TaxPeriodId,
        QttDeclarationId = existing?.DeclarationId,
        QttDeclarationStatus = existing?.Status,
        QttDraftRevision = existing?.DraftRevision
    };

    private static TknQttNextStepResponse WithSelectedChoice(
        TknQttNextStepResponse source,
        string choice,
        QttDeclarationResponse? declaration = null) => new()
    {
        TknTaxPeriodId = source.TknTaxPeriodId,
        TaxYear = source.TaxYear,
        AnnualRevenue = source.AnnualRevenue,
        IncomeBasedPitPaid = source.IncomeBasedPitPaid,
        Eligibility = source.Eligibility,
        RequiresPaymentSourceReview = source.RequiresPaymentSourceReview,
        CanCreateQttDraft = source.CanCreateQttDraft,
        Choices = source.Choices,
        BlockingIssues = source.BlockingIssues,
        SelectedChoice = choice,
        SelectedChoiceAt = source.SelectedChoiceAt,
        QttTaxPeriodId = declaration?.TaxPeriodId ?? source.QttTaxPeriodId,
        QttDeclarationId = declaration?.DeclarationId ?? source.QttDeclarationId,
        QttDeclarationStatus = declaration?.Status ?? source.QttDeclarationStatus,
        QttDraftRevision = declaration?.DraftRevision ?? source.QttDraftRevision
    };

    private async Task PersistBridgeChoiceAsync(
        TaxPeriod period,
        string choice,
        CancellationToken cancellationToken)
    {
        if (string.Equals(
                period.TknQttBridgeChoice,
                choice,
                StringComparison.Ordinal))
        {
            return;
        }

        var now = UtcNow();
        period.TknQttBridgeChoice = choice;
        period.TknQttBridgeChoiceAt = now;
        period.UpdatedAt = now;
        await _periods.SaveChangesAsync(cancellationToken);
    }

    private static bool HasSameOffsetAllocation(
        QttDeclarationResponse declaration,
        IReadOnlyList<QttOffsetAllocationItemRequest> requested,
        decimal overpaid)
    {
        if (declaration.Indicators.Indicator22 != 0m ||
            declaration.Indicators.Indicator23 != overpaid ||
            declaration.OffsetItems.Count != requested.Count)
        {
            return false;
        }

        return requested.All(item => declaration.OffsetItems.Any(existing =>
            existing.SourceObligationId == item.TaxDeclarationObligationId &&
            existing.ObligationIdentifier == item.ObligationIdentifier &&
            existing.OffsetAmount == item.OffsetAmount));
    }

    private static void EnsureYearEndBridgePeriod(TaxPeriod period)
    {
        if (period.FilingWindow == TknFilingWindows.FirstHalf)
        {
            throw new BadRequestException(
                "QTT overpayment choices are only available after a year-end TKN window.");
        }

        if (period.Status is not TaxPeriodStatuses.Submitted and
            not TaxPeriodStatuses.Paid)
        {
            throw new ConflictException(
                "Submit the TKN notification before reviewing the separate QTT next step.");
        }
    }

    private sealed record ExistingQttState(
        Guid TaxPeriodId,
        Guid DeclarationId,
        string Status,
        int DraftRevision);

    private async Task<(TaxPeriod Period, Guid OwnerId)> LoadAsync(Guid userId,
        Guid id, CancellationToken cancellationToken)
    {
        var period = await _periods.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("TKN tax period not found.");
        if (period.PeriodType != TaxPeriodTypes.Tkn)
            throw new BadRequestException("This endpoint only accepts TKN periods.");
        var anchor = await _periods.GetBusinessWithCategoryAsync(period.BusinessId, cancellationToken)
            ?? throw new NotFoundException("Anchor business not found.");
        if (anchor.OwnerId != userId)
            throw new ForbiddenException("You do not have permission to access this TKN period.");
        return (period, anchor.OwnerId);
    }

    private Task<OwnerRevenueProjection> GetRevenueProjectionAsync(
        Guid userId, TaxPeriod period, CancellationToken cancellationToken) =>
        _ownerRevenue.ProjectAsync(userId, period.BusinessId,
            period.PeriodStartDate, period.PeriodEndDate, cancellationToken);

    private async Task<TaxMate.Model.DTO.TaxPolicy.EffectiveTaxPolicyResponse> GetPolicyAsync(
        TaxPeriod period,
        CancellationToken cancellationToken)
    {
        var policyDate = DateOnly.FromDateTime(period.PeriodEndDate.AddDays(-1));
        var today = DateOnly.FromDateTime(UtcNow());
        return await _policies.GetEffectiveAsync(
            policyDate > today ? today : policyDate,
            cancellationToken);
    }

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private bool HasRevenueWindowEnded(TaxPeriod period)
    {
        var filingWindow = period.FilingWindow
            ?? throw new InvalidOperationException("TKN period has no filing window.");
        var window = TknPeriodWindow.Get(period.Year, filingWindow);
        var nowNaiveUtc = BangkokBusinessTime.NormalizeNaiveUtc(UtcNow());
        return nowNaiveUtc >= window.EndExclusiveNaiveUtc;
    }

    private void EnsureRevenueWindowEnded(TaxPeriod period)
    {
        if (!HasRevenueWindowEnded(period))
            throw new ConflictException(
                "TKN cannot be calculated before its revenue window has ended.");
    }

    private static IReadOnlyList<string> GetUnsupportedCategoryBlockers(
        IReadOnlyList<OwnerRevenueGroup> groups) => groups
        .Where(x => !SupportedIndicator08Categories.Contains(x.BusinessCategoryCode))
        .Select(x => string.Equals(
                x.BusinessCategoryCode,
                "ASSET_INSURANCE",
                StringComparison.OrdinalIgnoreCase)
            ? "Nhóm ASSET_INSURANCE đang gộp hoạt động cho thuê và bảo hiểm nên chưa thể đưa an toàn vào chỉ tiêu [08] của 01/TKN-CNKD."
            : $"Nhóm ngành '{x.BusinessCategoryCode}' chưa được hỗ trợ trên chỉ tiêu [08] của 01/TKN-CNKD.")
        .Distinct(StringComparer.Ordinal)
        .ToList();

    private static void EnsureSupportedCategories(
        IReadOnlyList<OwnerRevenueGroup> groups)
    {
        var blocker = GetUnsupportedCategoryBlockers(groups).FirstOrDefault();
        if (blocker is not null)
            throw new BadRequestException(blocker);
    }
}
