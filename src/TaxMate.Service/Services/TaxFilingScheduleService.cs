using TaxMate.Model.Common;
using TaxMate.Model.DTO.TaxFiling;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace TaxMate.Service.Services;

public sealed class TaxFilingScheduleService : ITaxFilingScheduleService
{
    private const string FilingType = "TKN";
    private readonly ITaxPeriodRepository _periods;
    private readonly ITaxPolicyService _taxPolicy;
    private readonly IOwnerRevenueProjector _ownerRevenue;
    private readonly IUnitOfWork _unitOfWork;

    public TaxFilingScheduleService(
        ITaxPeriodRepository periods,
        ITaxPolicyService taxPolicy,
        IOwnerRevenueProjector ownerRevenue,
        IUnitOfWork unitOfWork)
    {
        _periods = periods;
        _taxPolicy = taxPolicy;
        _ownerRevenue = ownerRevenue;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<TaxFilingTaskSummaryResponse>> GetTasksAsync(
        Guid userId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default)
    {
        ValidateYear(year);
        var schedule = await ResolveScheduleAsync(
            userId, businessId, year, cancellationToken);
        var tasks = new List<TaxFilingTaskSummaryResponse>();
        foreach (var filingWindow in schedule.AllowedWindows)
        {
            var context = await ResolveWindowContextAsync(
                schedule, filingWindow, cancellationToken);
            var period = await _periods.GetTknAsync(
                schedule.Owner.Id,
                year,
                filingWindow,
                cancellationToken);
            tasks.Add(BuildTask(context, period, DateTime.UtcNow));
        }

        return tasks;
    }

    public async Task<TaxFilingTaskSummaryResponse> OpenTaskAsync(
        Guid userId,
        Guid businessId,
        string taskId,
        CancellationToken cancellationToken = default)
    {
        var identity = ParseTaskId(taskId);
        var schedule = await ResolveScheduleAsync(
            userId, businessId, identity.Year, cancellationToken);

        if (!schedule.AllowedWindows.Contains(
                identity.FilingWindow,
                StringComparer.Ordinal))
        {
            throw new NotFoundException("Tax filing task not found.");
        }

        var context = await ResolveWindowContextAsync(
            schedule,
            identity.FilingWindow,
            cancellationToken);

        var existing = await _periods.GetTknAsync(
            context.Owner.Id,
            identity.Year,
            identity.FilingWindow,
            cancellationToken);
        var current = BuildTask(context, existing, DateTime.UtcNow);

        if (existing is not null &&
            current.Status == TaxFilingTaskStatuses.Completed)
        {
            return current;
        }

        if (!current.Eligibility.IsEligible)
        {
            throw new ConflictException(
                current.Eligibility.Blockers.First().Message);
        }

        if (current.Status == TaxFilingTaskStatuses.Upcoming)
        {
            throw new ConflictException(
                "Chưa đến thời gian mở hồ sơ thông báo doanh thu này.");
        }

        if (existing is not null)
        {
            return current;
        }

        var window = TknPeriodWindow.Get(
            identity.Year,
            identity.FilingWindow);
        var now = DateTime.UtcNow;
        var period = new TaxPeriod
        {
            Id = Guid.NewGuid(),
            BusinessId = context.Anchor.Id,
            PeriodType = TaxPeriodTypes.Tkn,
            FilingWindow = identity.FilingWindow,
            Year = identity.Year,
            PeriodStartDate = window.StartNaiveUtc,
            PeriodEndDate = window.EndExclusiveNaiveUtc,
            DueDate = window.DueDateNaiveUtc,
            Status = TaxPeriodStatuses.Open,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _periods.AddAsync(period);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var concurrent = await _periods.GetTknAsync(
                context.Owner.Id,
                identity.Year,
                identity.FilingWindow,
                cancellationToken);
            if (concurrent is null)
            {
                throw;
            }

            return BuildTask(context, concurrent, DateTime.UtcNow);
        }

        return BuildTask(context, period, now);
    }

    private async Task<ScheduleBase> ResolveScheduleAsync(
        Guid userId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken)
    {
        var requested = await _periods.GetBusinessWithCategoryAsync(
            businessId,
            cancellationToken);
        if (requested is null)
        {
            throw new NotFoundException("Business profile not found.");
        }

        if (requested.OwnerId != userId)
        {
            throw new ForbiddenException(
                "You do not have permission to access this business.");
        }

        var businesses = await _periods.GetBusinessesWithCategoriesByOwnerAsync(
            requested.OwnerId,
            cancellationToken);
        var anchor = businesses
            .Where(x => x.IsActive)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .FirstOrDefault();
        if (anchor is null)
        {
            throw new ConflictException(
                "Chủ hộ cần có ít nhất một cơ sở kinh doanh đang hoạt động.");
        }

        var owner = requested.Owner;
        var allowedWindows = ResolveAllowedWindows(owner, year);
        var revenue = await _ownerRevenue.ProjectCalendarYearAsync(
            userId,
            businessId,
            year,
            cancellationToken);
        var policy = await _taxPolicy.GetEffectiveAsync(
            new DateOnly(year, 12, 31),
            cancellationToken);

        return new ScheduleBase(
            owner,
            anchor,
            businesses,
            year,
            allowedWindows,
            revenue.TotalRevenue,
            revenue.Blockers,
            policy.AnnualRevenueThreshold);
    }

    private async Task<ScheduleContext> ResolveWindowContextAsync(
        ScheduleBase schedule,
        string filingWindow,
        CancellationToken cancellationToken)
    {
        var window = TknPeriodWindow.Get(schedule.Year, filingWindow);
        var projection = filingWindow == TknFilingWindows.Annual
            ? await _ownerRevenue.ProjectCalendarYearAsync(
                schedule.Owner.Id,
                schedule.Anchor.Id,
                schedule.Year,
                cancellationToken)
            : await _ownerRevenue.ProjectAsync(
                schedule.Owner.Id,
                schedule.Anchor.Id,
                window.StartNaiveUtc,
                window.EndExclusiveNaiveUtc,
                cancellationToken);

        return new ScheduleContext(
            schedule.Owner,
            schedule.Anchor,
            schedule.Year,
            filingWindow,
            projection.TotalRevenue,
            schedule.AnnualRevenue,
            filingWindow == TknFilingWindows.FirstHalf
                ? projection.Blockers
                : schedule.RevenueBlockers,
            schedule.Threshold);
    }

    private static IReadOnlyList<string> ResolveAllowedWindows(User owner, int year)
    {
        return owner.CommencementPeriod switch
        {
            CommencementPeriods.BeforeTaxYear => [TknFilingWindows.Annual],
            CommencementPeriods.FirstHalfOfTaxYear
                when owner.CommencementTaxYear == year =>
                    [TknFilingWindows.FirstHalf, TknFilingWindows.SecondHalf],
            CommencementPeriods.SecondHalfOfTaxYear
                when owner.CommencementTaxYear == year =>
                    [TknFilingWindows.SecondHalf],
            _ => [TknFilingWindows.Annual]
        };
    }

    private static TaxFilingTaskSummaryResponse BuildTask(
        ScheduleContext context,
        TaxPeriod? period,
        DateTime now)
    {
        var filingWindow = context.FilingWindow!;
        var window = TknPeriodWindow.Get(context.Year, filingWindow);
        var blockers = BuildBlockers(context, window, now);
        var eligible = blockers.Count == 0;
        var status = ResolveStatus(period, blockers, window, now);
        var action = ResolveAction(period, status, eligible);

        return new TaxFilingTaskSummaryResponse
        {
            TaskId = BuildTaskId(context.Year, filingWindow),
            FilingType = FilingType,
            FormCode = TaxFormCodes.Form01TknCnkd,
            TaxYear = context.Year,
            Window = new TaxFilingWindowResponse
            {
                Code = filingWindow,
                FromInclusive = ToBangkokDate(window.StartNaiveUtc),
                ToExclusive = ToBangkokDate(window.EndExclusiveNaiveUtc),
                Label = GetWindowLabel(filingWindow, context.Year)
            },
            Deadline = ToBangkokDate(window.DueDateNaiveUtc),
            Status = status,
            IsOverdue = period?.Status is not TaxPeriodStatuses.Submitted and
                not TaxPeriodStatuses.Paid &&
                DateOnly.FromDateTime(now.AddHours(7)) >
                    ToBangkokDate(window.DueDateNaiveUtc),
            Reason = ResolveReason(filingWindow),
            Eligibility = new TaxFilingEligibilityResponse
            {
                IsEligible = eligible,
                Blockers = blockers
            },
            PrimaryAction = action,
            TaxPeriodId = period?.Id,
            UpdatedAt = period?.UpdatedAt
        };
    }

    private static List<TaxFilingTaskBlockerResponse> BuildBlockers(
        ScheduleContext context,
        TknPeriodWindowValue window,
        DateTime now)
    {
        var blockers = new List<TaxFilingTaskBlockerResponse>();
        if (!context.Owner.TaxProfileConfirmedAt.HasValue)
        {
            blockers.Add(Blocker(
                TaxFilingTaskBlockerCodes.TaxProfileUnconfirmed,
                "Hãy xác nhận hồ sơ thuế trước khi mở thông báo doanh thu."));
        }

        if (context.Owner.DeclaredRevenueBracket != RevenueBrackets.AtOrBelow1B ||
            context.Owner.PersonalIncomeTaxMethod is not null)
        {
            blockers.Add(Blocker(
                TaxFilingTaskBlockerCodes.TaxProfileIncompatible,
                "Hồ sơ thuế hiện tại không thuộc diện thông báo doanh thu."));
        }

        if (context.Owner.CommencementPeriod is null ||
            !context.Owner.CommencementTaxYear.HasValue)
        {
            blockers.Add(Blocker(
                TaxFilingTaskBlockerCodes.CommencementDataMissing,
                "Hồ sơ thuế chưa có đủ thông tin thời điểm bắt đầu kinh doanh."));
        }

        var eligibilityRevenue = context.FilingWindow == TknFilingWindows.FirstHalf
            ? context.WindowRevenue
            : context.AnnualRevenue;
        if (eligibilityRevenue > context.Threshold)
        {
            blockers.Add(Blocker(
                TaxFilingTaskBlockerCodes.NotAtOrBelowThreshold,
                "Doanh thu trong kỳ thông báo đã vượt ngưỡng áp dụng."));
        }

        foreach (var sourceBlocker in context.RevenueBlockers)
        {
            blockers.Add(Blocker(
                TaxFilingTaskBlockerCodes.SourceDataInvalid,
                sourceBlocker.Message));
        }

        if (now < window.StartNaiveUtc)
        {
            blockers.Add(Blocker(
                TaxFilingTaskBlockerCodes.WindowNotStarted,
                "Chưa đến thời gian của kỳ thông báo doanh thu này."));
        }

        return blockers;
    }

    private static TaxFilingTaskBlockerResponse Blocker(
        string code,
        string message) => new() { Code = code, Message = message };

    private static string ResolveStatus(
        TaxPeriod? period,
        IReadOnlyCollection<TaxFilingTaskBlockerResponse> blockers,
        TknPeriodWindowValue window,
        DateTime now)
    {
        if (period?.Status is TaxPeriodStatuses.Submitted or TaxPeriodStatuses.Paid)
            return TaxFilingTaskStatuses.Completed;
        if (blockers.Any(x =>
                x.Code is TaxFilingTaskBlockerCodes.NotAtOrBelowThreshold or
                    TaxFilingTaskBlockerCodes.TaxProfileIncompatible))
            return TaxFilingTaskStatuses.NotApplicable;
        if (blockers.Count > 0)
            return now < window.StartNaiveUtc
                ? TaxFilingTaskStatuses.Upcoming
                : TaxFilingTaskStatuses.Blocked;
        return period is null
            ? TaxFilingTaskStatuses.Ready
            : TaxFilingTaskStatuses.InProgress;
    }

    private static TaxFilingTaskActionResponse ResolveAction(
        TaxPeriod? period,
        string status,
        bool eligible)
    {
        var code = status switch
        {
            TaxFilingTaskStatuses.Completed => TaxFilingTaskActions.View,
            _ when !eligible => TaxFilingTaskActions.None,
            _ when period is null => TaxFilingTaskActions.Open,
            _ => TaxFilingTaskActions.Continue
        };
        return new TaxFilingTaskActionResponse
        {
            Code = code,
            Enabled = code != TaxFilingTaskActions.None
        };
    }

    private static TaxFilingTaskReasonResponse ResolveReason(string window) =>
        window switch
        {
            TknFilingWindows.FirstHalf => new()
            {
                Code = TaxFilingTaskReasons.NewBusinessFirstHalf,
                Message = "Thông báo doanh thu cho hộ mới bắt đầu kinh doanh trong sáu tháng đầu năm."
            },
            TknFilingWindows.SecondHalf => new()
            {
                Code = TaxFilingTaskReasons.NewBusinessSecondHalf,
                Message = "Thông báo doanh thu cho hộ mới bắt đầu kinh doanh trong sáu tháng cuối năm."
            },
            _ => new()
            {
                Code = TaxFilingTaskReasons.AnnualAtOrBelowThreshold,
                Message = "Thông báo doanh thu năm cho hộ có doanh thu không quá ngưỡng áp dụng."
            }
        };

    private static string GetWindowLabel(string window, int year) => window switch
    {
        TknFilingWindows.FirstHalf => $"Sáu tháng đầu năm {year}",
        TknFilingWindows.SecondHalf => $"Sáu tháng cuối năm {year}",
        _ => $"Năm {year}"
    };

    private static DateOnly ToBangkokDate(DateTime naiveUtc) =>
        DateOnly.FromDateTime(naiveUtc.AddHours(7));

    private static string BuildTaskId(int year, string filingWindow) =>
        $"tkn-{year}-{filingWindow.ToLowerInvariant()}";

    private static (int Year, string FilingWindow) ParseTaskId(string taskId)
    {
        const string prefix = "tkn-";
        if (!taskId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new NotFoundException("Tax filing task not found.");

        var value = taskId[prefix.Length..];
        var separator = value.IndexOf('-');
        if (separator <= 0 ||
            !int.TryParse(value[..separator], out var year))
            throw new NotFoundException("Tax filing task not found.");

        var normalized = value[(separator + 1)..];
        var filingWindow = TknFilingWindows.All.FirstOrDefault(x =>
            string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));
        if (filingWindow is null)
            throw new NotFoundException("Tax filing task not found.");

        ValidateYear(year);
        return (year, filingWindow);
    }

    private static void ValidateYear(int year)
    {
        if (year is < 2000 or > 2100)
            throw new BadRequestException("Year must be between 2000 and 2100.");
    }

    private sealed record ScheduleContext(
        User Owner,
        BusinessProfile Anchor,
        int Year,
        string FilingWindow,
        decimal WindowRevenue,
        decimal AnnualRevenue,
        IReadOnlyList<OwnerRevenueBlocker> RevenueBlockers,
        decimal Threshold);

    private sealed record ScheduleBase(
        User Owner,
        BusinessProfile Anchor,
        IReadOnlyList<BusinessProfile> Businesses,
        int Year,
        IReadOnlyList<string> AllowedWindows,
        decimal AnnualRevenue,
        IReadOnlyList<OwnerRevenueBlocker> RevenueBlockers,
        decimal Threshold);
}
