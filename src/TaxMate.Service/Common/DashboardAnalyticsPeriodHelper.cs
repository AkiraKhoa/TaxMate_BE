using TaxMate.Model.DTO.Dashboard;

namespace TaxMate.Service.Common;

internal static class DashboardAnalyticsPeriodHelper
{
    public static DateTime UtcNow => DateTime.UtcNow;

    public static (DateTime Start, DateTime End) GetCurrentMonthToDate(DateTime utcNow)
    {
        var start = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (start, utcNow);
    }

    public static (DateTime Start, DateTime End) GetLastMonthSamePeriod(DateTime utcNow)
    {
        var (currentStart, currentEnd) = GetCurrentMonthToDate(utcNow);
        var duration = currentEnd - currentStart;
        var lastMonthStart = currentStart.AddMonths(-1);
        return (lastMonthStart, lastMonthStart + duration);
    }

    public static List<(int Year, int Month, DateTime Start, DateTime End)> GetRecentSixMonths(DateTime utcNow)
    {
        var (currentStart, _) = GetCurrentMonthToDate(utcNow);
        var months = new List<(int Year, int Month, DateTime Start, DateTime End)>();

        for (var offset = 5; offset >= 0; offset--)
        {
            var monthStart = currentStart.AddMonths(-offset);
            var monthEnd = offset == 0 ? utcNow : monthStart.AddMonths(1);
            months.Add((monthStart.Year, monthStart.Month, monthStart, monthEnd));
        }

        return months;
    }

    public static string BuildMonthLabel(int year, int month) => $"{year:D4}-{month:D2}";

    public static MomCountMetricDto BuildCountMetric(int current, int last)
    {
        return new MomCountMetricDto
        {
            CurrentMonth = current,
            LastMonth = last,
            DeltaPercent = CalculateDeltaPercent(current, last)
        };
    }

    public static MomRevenueMetricDto BuildRevenueMetric(decimal current, decimal last)
    {
        return new MomRevenueMetricDto
        {
            CurrentMonth = current,
            LastMonth = last,
            DeltaPercent = CalculateDeltaPercent(current, last)
        };
    }

    private static decimal? CalculateDeltaPercent(decimal current, decimal last)
    {
        if (last == 0)
        {
            return current == 0 ? 0 : null;
        }

        return Math.Round((current - last) / last * 100m, 2);
    }
}
