using TaxMate.Model.DTO.Dashboard;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class DashboardAnalyticsService : IDashboardAnalyticsService
{
    private readonly IDashboardAnalyticsRepository _dashboardAnalyticsRepository;

    public DashboardAnalyticsService(IDashboardAnalyticsRepository dashboardAnalyticsRepository)
    {
        _dashboardAnalyticsRepository = dashboardAnalyticsRepository;
    }

    public async Task<MomCountMetricDto> GetActiveBusinessesAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DashboardAnalyticsPeriodHelper.UtcNow;
        var (currentStart, currentEnd) = DashboardAnalyticsPeriodHelper.GetCurrentMonthToDate(utcNow);
        var (lastStart, lastEnd) = DashboardAnalyticsPeriodHelper.GetLastMonthSamePeriod(utcNow);

        var current = await _dashboardAnalyticsRepository.CountActiveBusinessesAsync(currentStart, currentEnd, cancellationToken);
        var last = await _dashboardAnalyticsRepository.CountActiveBusinessesAsync(lastStart, lastEnd, cancellationToken);

        return DashboardAnalyticsPeriodHelper.BuildCountMetric(current, last);
    }

    public async Task<MomCountMetricDto> GetPaidSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DashboardAnalyticsPeriodHelper.UtcNow;
        var (currentStart, currentEnd) = DashboardAnalyticsPeriodHelper.GetCurrentMonthToDate(utcNow);
        var (lastStart, lastEnd) = DashboardAnalyticsPeriodHelper.GetLastMonthSamePeriod(utcNow);

        var current = await _dashboardAnalyticsRepository.CountPaidSubscriptionsAsync(currentStart, currentEnd, cancellationToken);
        var last = await _dashboardAnalyticsRepository.CountPaidSubscriptionsAsync(lastStart, lastEnd, cancellationToken);

        return DashboardAnalyticsPeriodHelper.BuildCountMetric(current, last);
    }

    public async Task<MomRevenueMetricDto> GetMonthlyRevenueAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DashboardAnalyticsPeriodHelper.UtcNow;
        var (currentStart, currentEnd) = DashboardAnalyticsPeriodHelper.GetCurrentMonthToDate(utcNow);
        var (lastStart, lastEnd) = DashboardAnalyticsPeriodHelper.GetLastMonthSamePeriod(utcNow);

        var current = await _dashboardAnalyticsRepository.SumSubscriptionRevenueAsync(currentStart, currentEnd, cancellationToken);
        var last = await _dashboardAnalyticsRepository.SumSubscriptionRevenueAsync(lastStart, lastEnd, cancellationToken);

        return DashboardAnalyticsPeriodHelper.BuildRevenueMetric(current, last);
    }

    public async Task<SubscriptionTrendResponseDto> GetSubscriptionTrendAsync(CancellationToken cancellationToken = default)
    {
        var months = DashboardAnalyticsPeriodHelper.GetRecentSixMonths(DashboardAnalyticsPeriodHelper.UtcNow);
        var counts = await _dashboardAnalyticsRepository.GetSubscriptionCountsByMonthAsync(months, cancellationToken);

        return new SubscriptionTrendResponseDto
        {
            Points = months.Select(month => new MonthlyTrendPointDto
            {
                Year = month.Year,
                Month = month.Month,
                MonthLabel = DashboardAnalyticsPeriodHelper.BuildMonthLabel(month.Year, month.Month),
                Value = counts.TryGetValue((month.Year, month.Month), out var count) ? count : 0
            }).ToList()
        };
    }

    public async Task<ServicePackageDistributionResponseDto> GetServicePackageDistributionAsync(
        CancellationToken cancellationToken = default)
    {
        var months = DashboardAnalyticsPeriodHelper.GetRecentSixMonths(DashboardAnalyticsPeriodHelper.UtcNow);
        var distribution = await _dashboardAnalyticsRepository.GetPackageDistributionByMonthAsync(months, cancellationToken);
        var plans = await _dashboardAnalyticsRepository.GetSubscriptionPlansAsync(cancellationToken);

        return new ServicePackageDistributionResponseDto
        {
            Months = months.Select(month => new MonthlyPackageDistributionDto
            {
                Year = month.Year,
                Month = month.Month,
                MonthLabel = DashboardAnalyticsPeriodHelper.BuildMonthLabel(month.Year, month.Month),
                Packages = plans.Select(plan => new PackageDistributionItemDto
                {
                    PlanId = plan.PlanId,
                    PlanName = plan.PlanName,
                    Count = distribution.TryGetValue((month.Year, month.Month, plan.PlanId), out var count) ? count : 0
                }).ToList()
            }).ToList()
        };
    }

    public async Task<PackageRevenueResponseDto> GetPackageRevenueAsync(
        int? year = null,
        int? month = null,
        CancellationToken cancellationToken = default)
    {
        var utcNow = DashboardAnalyticsPeriodHelper.UtcNow;
        DateTime periodStart;
        DateTime periodEnd;
        int resolvedYear;
        int resolvedMonth;

        if (year.HasValue || month.HasValue)
        {
            resolvedYear = year ?? utcNow.Year;
            resolvedMonth = month ?? utcNow.Month;

            if (resolvedMonth is < 1 or > 12)
            {
                throw new BadRequestException("Tháng phải nằm trong khoảng 1–12.");
            }

            if (resolvedYear is < 2000 or > 2100)
            {
                throw new BadRequestException("Năm không hợp lệ.");
            }

            (periodStart, periodEnd) = DashboardAnalyticsPeriodHelper.GetMonthRange(
                resolvedYear,
                resolvedMonth);
        }
        else
        {
            (periodStart, periodEnd) = DashboardAnalyticsPeriodHelper.GetCurrentMonthToDate(utcNow);
            resolvedYear = utcNow.Year;
            resolvedMonth = utcNow.Month;
        }

        var plans = await _dashboardAnalyticsRepository.GetSubscriptionPlansAsync(cancellationToken);
        var rows = await _dashboardAnalyticsRepository.GetPackageRevenueAsync(
            periodStart,
            periodEnd,
            cancellationToken);
        var revenueByPlan = rows.ToDictionary(row => row.PlanId);

        var packages = plans
            .Select(plan =>
            {
                if (revenueByPlan.TryGetValue(plan.PlanId, out var row))
                {
                    return new PackageRevenueItemDto
                    {
                        PlanId = row.PlanId,
                        PlanName = row.PlanName,
                        SubscriptionCount = row.SubscriptionCount,
                        Revenue = row.Revenue
                    };
                }

                return new PackageRevenueItemDto
                {
                    PlanId = plan.PlanId,
                    PlanName = plan.PlanName,
                    SubscriptionCount = 0,
                    Revenue = 0
                };
            })
            .OrderByDescending(package => package.Revenue)
            .ThenBy(package => package.PlanName)
            .ToList();

        return new PackageRevenueResponseDto
        {
            Year = resolvedYear,
            Month = resolvedMonth,
            MonthLabel = DashboardAnalyticsPeriodHelper.BuildMonthLabel(resolvedYear, resolvedMonth),
            TotalRevenue = packages.Sum(package => package.Revenue),
            Packages = packages
        };
    }

    public async Task<BusinessUserTrendResponseDto> GetBusinessUserTrendAsync(CancellationToken cancellationToken = default)
    {
        var months = DashboardAnalyticsPeriodHelper.GetRecentSixMonths(DashboardAnalyticsPeriodHelper.UtcNow);
        var counts = await _dashboardAnalyticsRepository.GetDistinctSubscribedUsersByMonthAsync(months, cancellationToken);

        return new BusinessUserTrendResponseDto
        {
            Points = months.Select(month => new MonthlyTrendPointDto
            {
                Year = month.Year,
                Month = month.Month,
                MonthLabel = DashboardAnalyticsPeriodHelper.BuildMonthLabel(month.Year, month.Month),
                Value = counts.TryGetValue((month.Year, month.Month), out var count) ? count : 0
            }).ToList()
        };
    }

    public async Task<ChatMessageCountDto> GetTotalChatMessagesAsync(CancellationToken cancellationToken = default)
    {
        var total = await _dashboardAnalyticsRepository.CountAssistantChatMessagesAsync(
            null,
            null,
            cancellationToken);

        return new ChatMessageCountDto { Total = total };
    }

    public async Task<ChatMessageCountDto> GetTodayChatMessagesAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DashboardAnalyticsPeriodHelper.UtcNow;
        var (start, end) = DashboardAnalyticsPeriodHelper.GetTodayUtcRange(utcNow);
        var total = await _dashboardAnalyticsRepository.CountAssistantChatMessagesAsync(
            start,
            end,
            cancellationToken);

        return new ChatMessageCountDto { Total = total };
    }

    public async Task<AiAccuracyMetricDto> GetAiAccuracyAsync(CancellationToken cancellationToken = default)
    {
        var averageScore = await _dashboardAnalyticsRepository.GetAverageSimilarityScoreAsync(cancellationToken);
        var accuracyPercent = averageScore.HasValue
            ? Math.Round((decimal)averageScore.Value * 100m, 2)
            : 0m;

        return new AiAccuracyMetricDto { AccuracyPercent = accuracyPercent };
    }

    public async Task<UserConversionResponseDto> GetUserConversionAsync(CancellationToken cancellationToken = default)
    {
        var totalUsers = await _dashboardAnalyticsRepository.CountOwnerUsersAsync(cancellationToken);
        var plans = await _dashboardAnalyticsRepository.GetSubscriptionPlansAsync(cancellationToken);
        var usersByPlan = await _dashboardAnalyticsRepository.GetActiveUsersByPlanAsync(cancellationToken);
        var countByPlanId = usersByPlan.ToDictionary(row => row.PlanId, row => row.UserCount);

        decimal PercentOfTotal(int count) =>
            totalUsers == 0 ? 0m : Math.Round(count * 100m / totalUsers, 0);

        var stages = new List<UserConversionStageDto>
        {
            new()
            {
                PlanId = null,
                Label = "Tổng người dùng",
                Count = totalUsers,
                Percent = 100m
            }
        };

        stages.AddRange(plans.Select(plan =>
        {
            var count = countByPlanId.TryGetValue(plan.PlanId, out var userCount) ? userCount : 0;
            return new UserConversionStageDto
            {
                PlanId = plan.PlanId,
                Label = plan.PlanName,
                Count = count,
                Percent = PercentOfTotal(count)
            };
        }));

        return new UserConversionResponseDto
        {
            TotalUsers = totalUsers,
            Stages = stages
        };
    }
}
