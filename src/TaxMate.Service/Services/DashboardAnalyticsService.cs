using TaxMate.Model.DTO.Dashboard;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
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

    public async Task<PackageRevenueResponseDto> GetPackageRevenueAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DashboardAnalyticsPeriodHelper.UtcNow;
        var (currentStart, currentEnd) = DashboardAnalyticsPeriodHelper.GetCurrentMonthToDate(utcNow);
        var plans = await _dashboardAnalyticsRepository.GetSubscriptionPlansAsync(cancellationToken);
        var rows = await _dashboardAnalyticsRepository.GetPackageRevenueAsync(currentStart, currentEnd, cancellationToken);
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
            Year = utcNow.Year,
            Month = utcNow.Month,
            MonthLabel = DashboardAnalyticsPeriodHelper.BuildMonthLabel(utcNow.Year, utcNow.Month),
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
}
