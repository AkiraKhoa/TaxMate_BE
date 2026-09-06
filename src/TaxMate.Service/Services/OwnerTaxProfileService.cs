using TaxMate.Model.Common;
using TaxMate.Model.DTO.TaxProfile;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public sealed class OwnerTaxProfileService : IOwnerTaxProfileService
{
    private static readonly HashSet<string> CompletedPeriodStatuses = new(
        [
            TaxPeriodStatuses.Submitted,
            TaxPeriodStatuses.PartiallyPaid,
            TaxPeriodStatuses.Paid
        ],
        StringComparer.Ordinal);

    private readonly IUserRepository _users;
    private readonly ITaxPeriodRepository _periods;
    private readonly IOwnerRevenueProjector _ownerRevenue;
    private readonly ITaxPolicyService _policies;
    private readonly IRevenueThresholdAlertService _thresholdEvaluator;
    private readonly IGenericRepository<RevenueThresholdAlert> _alerts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public OwnerTaxProfileService(
        IUserRepository users,
        ITaxPeriodRepository periods,
        IOwnerRevenueProjector ownerRevenue,
        ITaxPolicyService policies,
        IRevenueThresholdAlertService thresholdEvaluator,
        IGenericRepository<RevenueThresholdAlert> alerts,
        IUnitOfWork unitOfWork,
        TimeProvider? timeProvider = null)
    {
        _users = users;
        _periods = periods;
        _ownerRevenue = ownerRevenue;
        _policies = policies;
        _thresholdEvaluator = thresholdEvaluator;
        _alerts = alerts;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<OwnerTaxProfileResponse> GetCurrentAsync(
        Guid userId,
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        var (owner, _) = await LoadOwnerAsync(
            userId, businessId, cancellationToken);
        var year = CurrentBangkokYear();
        var reviews = await GetThresholdReviewsAsync(
            userId, businessId, year, cancellationToken);
        var methodLock = await ResolveMethodLockAsync(
            owner, year, cancellationToken);
        var allReviews = reviews.ToList();

        // RevenueBased may cross 3B late in a year but remains valid through that
        // year. Keep its acknowledged transition visible in the next year until
        // IncomeBased is activated, instead of looking only at current-year sales.
        var deferred = (await _alerts.FindAsync(x =>
                x.OwnerId == owner.Id &&
                x.ThresholdCode == RevenueThresholdCodes.Crossed3B &&
                x.Status == RevenueThresholdAlertStatuses.Acknowledged &&
                x.Year < year))
            .OrderByDescending(x => x.Year)
            .ToList();
        foreach (var alert in deferred)
        {
            var projection = await _ownerRevenue.ProjectCalendarYearAsync(
                owner.Id, businessId, alert.Year, cancellationToken);
            var policy = await _policies.GetEffectiveAsync(
                new DateOnly(alert.Year, 12, 31), cancellationToken);
            var review = BuildThresholdReview(
                owner, alert, projection.TotalRevenue, policy,
                methodLock, year);
            if (review.CanConfirm)
                allReviews.Add(review);
        }

        return MapProfile(owner, businessId, methodLock,
            allReviews.OrderBy(x => x.Year)
                .ThenBy(x => x.ThresholdAmount)
                .ToList());
    }

    public async Task<OwnerTaxProfileResponse> UpdateCurrentAsync(
        Guid userId,
        Guid businessId,
        UpdateOwnerTaxProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Confirmed)
            throw new BadRequestException(
                "Bạn phải xác nhận thông tin hồ sơ thuế.");

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var (owner, _) = await LoadOwnerAsync(
                userId, businessId, cancellationToken);
            if (await _periods.HasOwnerTaxArtifactsAsync(
                    owner.Id, cancellationToken))
            {
                throw new ConflictException(
                    "Hồ sơ thuế đã có kỳ tính hoặc tờ khai; hãy dùng luồng chuyển diện.");
            }

            ApplyInitialProfile(owner, request, CurrentBangkokYear());
            var now = UtcNow();
            owner.TaxProfileConfirmedAt = now;
            owner.UpdatedAt = now;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return await GetCurrentAsync(
                userId, businessId, cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<RevenueThresholdReviewResponse>>
        GetThresholdReviewsAsync(
            Guid userId,
            Guid businessId,
            int year,
            CancellationToken cancellationToken = default)
    {
        ValidateTaxYear(year);
        var (owner, _) = await LoadOwnerAsync(
            userId, businessId, cancellationToken);
        var alerts = await _thresholdEvaluator.EvaluateAsync(
            owner.Id, businessId, year, cancellationToken);
        var projection = await _ownerRevenue.ProjectCalendarYearAsync(
            owner.Id, businessId, year, cancellationToken);
        var policy = await _policies.GetEffectiveAsync(
            new DateOnly(year, 12, 31), cancellationToken);
        var methodLock = await ResolveMethodLockAsync(
            owner, year, cancellationToken);
        return alerts
            .Select(x => BuildThresholdReview(
                owner, x, projection.TotalRevenue, policy,
                methodLock, CurrentBangkokYear()))
            .Where(x => x.Status != RevenueThresholdAlertStatuses.Resolved ||
                        x.CurrentAnnualRevenue > x.ThresholdAmount)
            .OrderBy(x => x.ThresholdAmount)
            .ToList();
    }

    public async Task<RevenueThresholdReviewResponse>
        ConfirmThresholdReviewAsync(
            Guid userId,
            Guid businessId,
            Guid alertId,
            ConfirmRevenueThresholdReviewRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Confirmed)
            throw new BadRequestException(
                "Bạn phải xác nhận lựa chọn chuyển diện.");

        var (owner, _) = await LoadOwnerAsync(
            userId, businessId, cancellationToken);
        var alertBeforeReconcile = await LoadAlertAsync(owner.Id, alertId);
        await _thresholdEvaluator.EvaluateAsync(
            owner.Id, businessId, alertBeforeReconcile.Year, cancellationToken);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var alert = await LoadAlertAsync(owner.Id, alertId);
            var projection = await _ownerRevenue.ProjectCalendarYearAsync(
                owner.Id, businessId, alert.Year, cancellationToken);
            var policy = await _policies.GetEffectiveAsync(
                new DateOnly(alert.Year, 12, 31), cancellationToken);
            var methodLock = await ResolveMethodLockAsync(
                owner, alert.Year, cancellationToken);
            var review = BuildThresholdReview(
                owner, alert, projection.TotalRevenue, policy,
                methodLock, CurrentBangkokYear());
            if (!review.CanConfirm)
                throw new ConflictException(review.Message);

            var now = UtcNow();
            if (alert.ThresholdCode == RevenueThresholdCodes.Crossed50B)
            {
                alert.Status = RevenueThresholdAlertStatuses.Acknowledged;
            }
            else if (alert.ThresholdCode == RevenueThresholdCodes.Crossed1B)
            {
                var hasExistingMethod = owner.PersonalIncomeTaxMethod is not null &&
                                        owner.TaxMethodEffectiveYear.HasValue;
                var method = review.RequiredTaxMethod ??
                    NormalizeSelectedMethod(request.PersonalIncomeTaxMethod,
                        review.AllowedTaxMethods);
                owner.DeclaredRevenueBracket =
                    method == PersonalIncomeTaxMethods.IncomeBased &&
                    projection.TotalRevenue > policy.IncomeBasedRequirementThreshold
                        ? RevenueBrackets.Over3BTo50B
                        : RevenueBrackets.Over1BTo3B;
                owner.PersonalIncomeTaxMethod = method;
                if (!hasExistingMethod)
                    owner.TaxMethodEffectiveYear = methodLock.IsLocked
                        ? methodLock.EffectiveYear
                        : alert.Year;
                owner.CommencementPeriod = null;
                owner.CommencementTaxYear = null;
                owner.TaxProfileConfirmedAt = now;
                owner.UpdatedAt = now;
                Resolve(alert, now);
            }
            else if (alert.ThresholdCode == RevenueThresholdCodes.Crossed3B)
            {
                if (owner.PersonalIncomeTaxMethod ==
                        PersonalIncomeTaxMethods.RevenueBased &&
                    CurrentBangkokYear() <= alert.Year)
                {
                    alert.Status = RevenueThresholdAlertStatuses.Acknowledged;
                }
                else
                {
                    var wasRevenueBased = owner.PersonalIncomeTaxMethod ==
                        PersonalIncomeTaxMethods.RevenueBased;
                    owner.DeclaredRevenueBracket = RevenueBrackets.Over3BTo50B;
                    owner.PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.IncomeBased;
                    owner.TaxMethodEffectiveYear = wasRevenueBased
                        ? alert.Year + 1
                        : owner.TaxMethodEffectiveYear ?? alert.Year;
                    owner.CommencementPeriod = null;
                    owner.CommencementTaxYear = null;
                    owner.TaxProfileConfirmedAt = now;
                    owner.UpdatedAt = now;
                    Resolve(alert, now);
                }
            }

            alert.UpdatedAt = now;
            _alerts.Update(alert);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            var refreshedLock = await ResolveMethodLockAsync(
                owner, alert.Year, cancellationToken);
            return BuildThresholdReview(
                owner, alert, projection.TotalRevenue, policy,
                refreshedLock, CurrentBangkokYear());
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<RevenueThresholdReviewResponse>
        DismissThresholdReviewAsync(
            Guid userId,
            Guid businessId,
            Guid alertId,
            CancellationToken cancellationToken = default)
    {
        var (owner, _) = await LoadOwnerAsync(
            userId, businessId, cancellationToken);
        var alert = await LoadAlertAsync(owner.Id, alertId);
        var projection = await _ownerRevenue.ProjectCalendarYearAsync(
            owner.Id, businessId, alert.Year, cancellationToken);
        if (projection.TotalRevenue > alert.ThresholdAmount)
            throw new ConflictException(
                "Doanh thu hiện vẫn vượt ngưỡng nên không thể bỏ qua cảnh báo.");

        alert.Status = RevenueThresholdAlertStatuses.Acknowledged;
        alert.TotalRevenue = projection.TotalRevenue;
        alert.WindowEnd = projection.EndExclusiveNaiveUtc;
        alert.ResolvedAt = null;
        alert.UpdatedAt = UtcNow();
        _alerts.Update(alert);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var policy = await _policies.GetEffectiveAsync(
            new DateOnly(alert.Year, 12, 31), cancellationToken);
        var methodLock = await ResolveMethodLockAsync(
            owner, alert.Year, cancellationToken);
        return BuildThresholdReview(
            owner, alert, projection.TotalRevenue, policy,
            methodLock, CurrentBangkokYear());
    }

    public async Task<AnnualRevenueConclusionPreviewResponse>
        PreviewAnnualConclusionAsync(
            Guid userId,
            Guid businessId,
            int taxYear,
            CancellationToken cancellationToken = default)
    {
        ValidateTaxYear(taxYear);
        var (owner, _) = await LoadOwnerAsync(
            userId, businessId, cancellationToken);
        return await BuildPreviewAsync(
            owner, businessId, taxYear, cancellationToken);
    }

    public async Task<AnnualRevenueConclusionPreviewResponse>
        ConfirmAnnualConclusionAsync(
            Guid userId,
            Guid businessId,
            int taxYear,
            ConfirmAnnualRevenueConclusionRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Confirmed)
            throw new BadRequestException(
                "Bạn phải xác nhận đã rà soát kết luận doanh thu năm.");

        ValidateTaxYear(taxYear);
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var (owner, _) = await LoadOwnerAsync(
                userId, businessId, cancellationToken);
            var preview = await BuildPreviewAsync(
                owner, businessId, taxYear, cancellationToken);

            if (preview.AlreadyConfirmed)
            {
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
                return preview;
            }

            if (!preview.CanConfirm)
            {
                throw new ConflictException(
                    preview.BlockingIssues.FirstOrDefault()?.Message ??
                    "Chưa đủ điều kiện kết luận doanh thu năm không quá 1 tỷ đồng.");
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            ApplyAnnualProfile(owner, preview, request, taxYear);
            owner.TaxProfileConfirmedAt = now;
            owner.UpdatedAt = now;

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return await BuildPreviewAsync(
                owner, businessId, taxYear, cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private static OwnerTaxProfileResponse MapProfile(
        User owner,
        Guid businessId,
        MethodLockState methodLock,
        IReadOnlyList<RevenueThresholdReviewResponse> reviews) => new()
    {
        BusinessId = businessId,
        DeclaredRevenueBracket = owner.DeclaredRevenueBracket,
        PersonalIncomeTaxMethod = owner.PersonalIncomeTaxMethod,
        TaxMethodEffectiveYear = owner.TaxMethodEffectiveYear,
        CommencementPeriod = owner.CommencementPeriod,
        CommencementTaxYear = owner.CommencementTaxYear,
        ConfirmedAt = owner.TaxProfileConfirmedAt,
        IsConfigured = owner.DeclaredRevenueBracket is not null &&
                       owner.TaxProfileConfirmedAt.HasValue,
        IsMethodLocked = methodLock.IsLocked,
        LockedThroughYear = methodLock.IsLocked
            ? methodLock.LockedThroughYear
            : null,
        ThresholdReviews = reviews
    };

    private static void ApplyInitialProfile(
        User owner,
        UpdateOwnerTaxProfileRequest request,
        int currentYear)
    {
        var bracket = request.DeclaredRevenueBracket?.Trim();
        if (bracket is null || !RevenueBrackets.All.Contains(bracket))
            throw new BadRequestException("Nhóm doanh thu không hợp lệ.");

        owner.DeclaredRevenueBracket = bracket;
        if (bracket == RevenueBrackets.AtOrBelow1B)
        {
            var commencement = request.CommencementPeriod?.Trim();
            if (commencement is null ||
                !CommencementPeriods.All.Contains(commencement) ||
                !request.CommencementTaxYear.HasValue)
            {
                throw new BadRequestException(
                    "Diện không quá 1 tỷ phải có thời điểm bắt đầu kinh doanh.");
            }
            if (request.CommencementTaxYear.Value is < 2000 or > 9998 ||
                request.CommencementTaxYear.Value > currentYear)
            {
                throw new BadRequestException(
                    "Năm bắt đầu kinh doanh không hợp lệ.");
            }
            owner.PersonalIncomeTaxMethod = null;
            owner.TaxMethodEffectiveYear = null;
            owner.CommencementPeriod = commencement;
            owner.CommencementTaxYear = request.CommencementTaxYear;
            return;
        }

        owner.CommencementPeriod = null;
        owner.CommencementTaxYear = null;
        if (bracket == RevenueBrackets.Over3BTo50B)
        {
            owner.PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.IncomeBased;
            owner.TaxMethodEffectiveYear = currentYear;
            return;
        }

        var method = NormalizeSelectedMethod(
            request.PersonalIncomeTaxMethod,
            SelectableMethods);
        owner.PersonalIncomeTaxMethod = method;
        owner.TaxMethodEffectiveYear = currentYear;
    }

    private async Task<MethodLockState> ResolveMethodLockAsync(
        User owner,
        int reviewYear,
        CancellationToken cancellationToken)
    {
        if (owner.PersonalIncomeTaxMethod == PersonalIncomeTaxMethods.IncomeBased &&
            owner.TaxMethodEffectiveYear.HasValue)
        {
            var through = owner.TaxMethodEffectiveYear.Value + 1;
            return new MethodLockState(
                reviewYear <= through,
                owner.TaxMethodEffectiveYear.Value,
                through);
        }

        if (owner.PersonalIncomeTaxMethod is not null)
            return MethodLockState.None;

        var historical = await _periods.GetOwnerTaxMethodHistoryAsync(
            owner.Id, cancellationToken);
        var active = historical.FirstOrDefault(x =>
            x.TaxMethod == PersonalIncomeTaxMethods.IncomeBased &&
            reviewYear <= x.TaxMethodEffectiveYear + 1);
        return active is null
            ? MethodLockState.None
            : new MethodLockState(
                true,
                active.TaxMethodEffectiveYear,
                active.TaxMethodEffectiveYear + 1);
    }

    private static RevenueThresholdReviewResponse BuildThresholdReview(
        User owner,
        RevenueThresholdAlert alert,
        decimal currentRevenue,
        TaxMate.Model.DTO.TaxPolicy.EffectiveTaxPolicyResponse policy,
        MethodLockState methodLock,
        int currentYear)
    {
        var outsideScope = currentRevenue > policy.SupportedRevenueCeiling;
        var isCrossed1 = alert.ThresholdCode == RevenueThresholdCodes.Crossed1B;
        var isCrossed3 = alert.ThresholdCode == RevenueThresholdCodes.Crossed3B;
        var isCrossed50 = alert.ThresholdCode == RevenueThresholdCodes.Crossed50B;
        // A 1B alert does not elect a new method for an already configured owner.
        // Changes after crossing 3B or expiry of a lock use their dedicated flows.
        var existingMethod = isCrossed1 && owner.TaxMethodEffectiveYear.HasValue
            ? owner.PersonalIncomeTaxMethod
            : null;
        var requiredMethod = existingMethod ?? (isCrossed3 ||
            (isCrossed1 &&
             (currentRevenue > policy.IncomeBasedRequirementThreshold ||
              methodLock.IsLocked))
                ? PersonalIncomeTaxMethods.IncomeBased
                : null);
        IReadOnlyList<string> allowedMethods = existingMethod is null && isCrossed1 &&
            currentRevenue <= policy.IncomeBasedRequirementThreshold &&
            !methodLock.IsLocked
                ? SelectableMethods
                : requiredMethod is null ? [] : [requiredMethod];
        var deferredRevenueBased = isCrossed3 &&
            owner.PersonalIncomeTaxMethod == PersonalIncomeTaxMethods.RevenueBased;
        var canActivateDeferred = deferredRevenueBased && currentYear > alert.Year;
        var isExceeded = currentRevenue > alert.ThresholdAmount;
        var canConfirm = isExceeded &&
            (alert.Status == RevenueThresholdAlertStatuses.PendingReview ||
             (alert.Status == RevenueThresholdAlertStatuses.Acknowledged &&
              canActivateDeferred)) &&
            (!outsideScope || isCrossed50);

        var message = !isExceeded
            ? "Doanh thu hiện không còn vượt mốc này; có thể đóng cảnh báo."
            : outsideScope && !isCrossed50
                ? "Doanh thu đã vượt 50 tỷ đồng; hãy xử lý cảnh báo ngoài phạm vi hỗ trợ."
                : isCrossed50
                    ? "Doanh thu đã vượt phạm vi TaxMate hỗ trợ lập hồ sơ thuế."
                    : deferredRevenueBased && currentYear <= alert.Year
                        ? $"Năm {alert.Year} tiếp tục RevenueBased; IncomeBased bắt buộc từ năm {alert.Year + 1}."
                        : methodLock.IsLocked && isCrossed1
                            ? $"IncomeBased còn ổn định đến hết năm {methodLock.LockedThroughYear}."
                            : "Hãy xác nhận phương pháp và nhóm doanh thu áp dụng.";

        return new RevenueThresholdReviewResponse
        {
            AlertId = alert.Id,
            Year = alert.Year,
            Quarter = alert.Quarter,
            ThresholdCode = alert.ThresholdCode,
            ThresholdAmount = alert.ThresholdAmount,
            CurrentAnnualRevenue = currentRevenue,
            Status = alert.Status,
            CanConfirm = canConfirm,
            CanDismiss = !isExceeded &&
                         alert.Status != RevenueThresholdAlertStatuses.Resolved,
            RequiredTaxMethod = requiredMethod,
            AllowedTaxMethods = allowedMethods,
            AppliesFromYear = deferredRevenueBased ? alert.Year + 1 : alert.Year,
            IsOutsideSupportedScope = outsideScope,
            Message = message
        };
    }

    private async Task<RevenueThresholdAlert> LoadAlertAsync(
        Guid ownerId,
        Guid alertId)
    {
        var alert = await _alerts.GetByIdAsync(alertId)
            ?? throw new NotFoundException("Không tìm thấy cảnh báo doanh thu.");
        if (alert.OwnerId != ownerId)
            throw new NotFoundException("Không tìm thấy cảnh báo doanh thu.");
        return alert;
    }

    private static string NormalizeSelectedMethod(
        string? selected,
        IReadOnlyCollection<string> allowed)
    {
        var method = selected?.Trim();
        if (method is null || !allowed.Contains(method))
            throw new BadRequestException(
                "Phương pháp TNCN không hợp lệ cho lần chuyển diện này.");
        return method;
    }

    private static void Resolve(RevenueThresholdAlert alert, DateTime now)
    {
        alert.Status = RevenueThresholdAlertStatuses.Resolved;
        alert.ResolvedAt = now;
    }

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private static readonly IReadOnlyList<string> SelectableMethods =
    [
        PersonalIncomeTaxMethods.RevenueBased,
        PersonalIncomeTaxMethods.IncomeBased
    ];

    private int CurrentBangkokYear()
    {
        var naiveUtc = BangkokBusinessTime.NormalizeNaiveUtc(UtcNow());
        return BangkokBusinessTime.GetBangkokCalendarYear(naiveUtc);
    }

    private sealed record MethodLockState(
        bool IsLocked,
        int? EffectiveYear,
        int? LockedThroughYear)
    {
        public static MethodLockState None { get; } = new(false, null, null);
    }

    private async Task<AnnualRevenueConclusionPreviewResponse> BuildPreviewAsync(
        User owner,
        Guid businessId,
        int taxYear,
        CancellationToken cancellationToken)
    {
        var projection = await _ownerRevenue.ProjectCalendarYearAsync(
            owner.Id, businessId, taxYear, cancellationToken);
        var policy = await _policies.GetEffectiveAsync(
            new DateOnly(taxYear, 12, 31), cancellationToken);
        var states = await _periods.GetOwnerQuarterlyFilingStatesAsync(
            owner.Id, taxYear, cancellationToken);
        var targetBracket = projection.TotalRevenue <= policy.AnnualRevenueThreshold
            ? RevenueBrackets.AtOrBelow1B
            : projection.TotalRevenue <= policy.IncomeBasedRequirementThreshold
                ? RevenueBrackets.Over1BTo3B
                : RevenueBrackets.Over3BTo50B;
        var nextYearLock = await ResolveMethodLockAsync(
            owner, taxYear + 1, cancellationToken);
        var requiredMethod = targetBracket == RevenueBrackets.Over3BTo50B ||
                             (targetBracket == RevenueBrackets.Over1BTo3B &&
                              nextYearLock.IsLocked)
            ? PersonalIncomeTaxMethods.IncomeBased
            : null;
        IReadOnlyList<string> allowedMethods =
            targetBracket == RevenueBrackets.Over1BTo3B && requiredMethod is null
                ? SelectableMethods
                : requiredMethod is null ? [] : [requiredMethod];
        var requiresUnlockedChoice =
            targetBracket == RevenueBrackets.Over1BTo3B &&
            owner.PersonalIncomeTaxMethod == PersonalIncomeTaxMethods.IncomeBased &&
            !nextYearLock.IsLocked;
        var alreadyConfirmed = targetBracket == RevenueBrackets.AtOrBelow1B
            ? IsAnnualTknProfile(owner, taxYear)
            : owner.DeclaredRevenueBracket == targetBracket &&
              owner.PersonalIncomeTaxMethod is not null &&
              owner.TaxMethodEffectiveYear.HasValue &&
              owner.TaxMethodEffectiveYear.Value <= taxYear + 1 &&
              !requiresUnlockedChoice;
        var shouldShow = owner.TaxProfileConfirmedAt.HasValue;
        var issues = projection.Blockers
            .Select(x => new AnnualRevenueConclusionIssue(x.Code, x.Message))
            .ToList();

        if (!HasTaxYearEnded(taxYear))
            issues.Add(new(
                "TaxYearNotEnded",
                $"Chỉ có thể kết luận doanh thu năm {taxYear} sau khi năm đã kết thúc."));

        if (!shouldShow)
            issues.Add(new(
                "TaxProfileIncompatible",
                "Hãy xác nhận hồ sơ thuế trước khi kết luận doanh thu năm."));

        if (projection.TotalRevenue > policy.SupportedRevenueCeiling)
            issues.Add(new(
                "AnnualRevenueOver50B",
                "Doanh thu năm vượt 50 tỷ đồng, ngoài phạm vi TaxMate hỗ trợ."));

        var requiredQuarters = ResolveRequiredQuarters(
            owner, taxYear, projection, policy.AnnualRevenueThreshold);
        var quarterResults = requiredQuarters
            .Select(quarter => BuildQuarter(
                quarter, states, owner.PersonalIncomeTaxMethod))
            .ToList();
        foreach (var quarter in quarterResults.Where(x => !x.IsReady))
            issues.Add(new(
                $"Quarter{quarter.Quarter}NotCompleted",
                $"Quý {quarter.Quarter} chưa tính đúng phương pháp và gửi 01/CNKD."));

        issues = issues
            .GroupBy(x => x.Code, StringComparer.Ordinal)
            .Select(x => x.First())
            .ToList();

        return new AnnualRevenueConclusionPreviewResponse
        {
            BusinessId = businessId,
            TaxYear = taxYear,
            AnnualRevenue = projection.TotalRevenue,
            RevenueThreshold = policy.AnnualRevenueThreshold,
            ShouldShow = shouldShow && !alreadyConfirmed,
            CanConfirm = !alreadyConfirmed && issues.Count == 0,
            AlreadyConfirmed = alreadyConfirmed,
            CurrentRevenueBracket = owner.DeclaredRevenueBracket,
            CurrentTaxMethod = owner.PersonalIncomeTaxMethod,
            TargetRevenueBracket = targetBracket,
            RequiredTaxMethod = requiredMethod,
            AllowedTaxMethods = allowedMethods,
            AppliesFromYear = targetBracket == RevenueBrackets.AtOrBelow1B
                ? taxYear
                : taxYear + 1,
            Quarters = quarterResults,
            BlockingIssues = alreadyConfirmed ? [] : issues
        };
    }

    private static AnnualRevenueConclusionQuarter BuildQuarter(
        int quarter,
        IReadOnlyList<OwnerQuarterlyFilingState> states,
        string? expectedMethod)
    {
        var candidates = states.Where(x => x.Quarter == quarter).ToList();
        var ready = candidates.FirstOrDefault(x =>
            CompletedPeriodStatuses.Contains(x.PeriodStatus) &&
            (expectedMethod == PersonalIncomeTaxMethods.IncomeBased
                ? x.HasCompletedIncomeBasedCalculation
                : x.HasCompletedRevenueBasedCalculation) &&
            x.HasSubmittedDeclaration);
        var selected = ready ?? candidates.FirstOrDefault();
        return new AnnualRevenueConclusionQuarter(
            quarter,
            selected?.TaxPeriodId,
            selected?.PeriodStatus,
            ready is not null);
    }

    private static IReadOnlyList<int> ResolveRequiredQuarters(
        User owner,
        int taxYear,
        OwnerRevenueProjection projection,
        decimal threshold)
    {
        if (owner.PersonalIncomeTaxMethod is null ||
            !owner.TaxMethodEffectiveYear.HasValue ||
            owner.TaxMethodEffectiveYear.Value > taxYear)
            return [];
        if (owner.TaxMethodEffectiveYear.Value < taxYear)
            return [1, 2, 3, 4];

        decimal cumulative = 0m;
        var crossing = projection.Lines
            .OrderBy(x => x.DocumentDate)
            .ThenBy(x => x.SourceType)
            .ThenBy(x => x.SourceId)
            .FirstOrDefault(x =>
            {
                cumulative += x.Amount;
                return cumulative > threshold;
            });
        var firstQuarter = crossing is null
            ? 1
            : ((crossing.DocumentDate.AddHours(7).Month - 1) / 3) + 1;
        return Enumerable.Range(firstQuarter, 5 - firstQuarter).ToList();
    }

    private static void ApplyAnnualProfile(
        User owner,
        AnnualRevenueConclusionPreviewResponse preview,
        ConfirmAnnualRevenueConclusionRequest request,
        int taxYear)
    {
        owner.DeclaredRevenueBracket = preview.TargetRevenueBracket;
        if (preview.TargetRevenueBracket == RevenueBrackets.AtOrBelow1B)
        {
            owner.PersonalIncomeTaxMethod = null;
            owner.TaxMethodEffectiveYear = null;
            owner.CommencementPeriod = CommencementPeriods.BeforeTaxYear;
            owner.CommencementTaxYear = taxYear;
            return;
        }

        owner.CommencementPeriod = null;
        owner.CommencementTaxYear = null;
        var method = preview.RequiredTaxMethod ?? NormalizeSelectedMethod(
            request.PersonalIncomeTaxMethod, preview.AllowedTaxMethods);
        var keepEffectiveYear = preview.RequiredTaxMethod is not null &&
                                owner.PersonalIncomeTaxMethod == method &&
                                owner.TaxMethodEffectiveYear.HasValue;
        owner.PersonalIncomeTaxMethod = method;
        owner.TaxMethodEffectiveYear = keepEffectiveYear
            ? owner.TaxMethodEffectiveYear
            : taxYear + 1;
    }

    private async Task<(User Owner, BusinessProfile Anchor)> LoadOwnerAsync(
        Guid userId,
        Guid businessId,
        CancellationToken cancellationToken)
    {
        var anchor = await _periods.GetBusinessWithCategoryAsync(
            businessId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy cơ sở kinh doanh.");
        if (anchor.OwnerId != userId)
            throw new ForbiddenException(
                "Bạn không có quyền kết luận hồ sơ thuế của chủ hộ này.");
        var owner = await _users.GetByIdAsync(userId)
            ?? throw new NotFoundException("Không tìm thấy chủ hộ.");
        return (owner, anchor);
    }

    private bool HasTaxYearEnded(int taxYear)
    {
        var now = BangkokBusinessTime.NormalizeNaiveUtc(
            _timeProvider.GetUtcNow().UtcDateTime);
        return now >= TknPeriodWindow.Get(
            taxYear, TknFilingWindows.Annual).EndExclusiveNaiveUtc;
    }

    private static bool IsAnnualTknProfile(User owner, int taxYear) =>
        owner.DeclaredRevenueBracket == RevenueBrackets.AtOrBelow1B &&
        owner.PersonalIncomeTaxMethod is null &&
        owner.TaxMethodEffectiveYear is null &&
        owner.CommencementPeriod == CommencementPeriods.BeforeTaxYear &&
        owner.CommencementTaxYear == taxYear &&
        owner.TaxProfileConfirmedAt.HasValue;

    private static void ValidateTaxYear(int taxYear)
    {
        if (taxYear is < 2000 or > 9998)
            throw new BadRequestException("TaxYear không hợp lệ.");
    }
}
