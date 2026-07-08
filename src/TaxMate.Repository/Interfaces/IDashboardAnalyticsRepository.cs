namespace TaxMate.Repository.Interfaces;

public interface IDashboardAnalyticsRepository
{
    Task<int> CountActiveBusinessesAsync(DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default);

    Task<int> CountPaidSubscriptionsAsync(DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default);

    Task<decimal> SumSubscriptionRevenueAsync(DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default);

    Task<Dictionary<(int Year, int Month), int>> GetSubscriptionCountsByMonthAsync(
        IReadOnlyList<(int Year, int Month, DateTime Start, DateTime End)> months,
        CancellationToken cancellationToken = default);

    Task<Dictionary<(int Year, int Month), int>> GetDistinctSubscribedUsersByMonthAsync(
        IReadOnlyList<(int Year, int Month, DateTime Start, DateTime End)> months,
        CancellationToken cancellationToken = default);

    Task<Dictionary<(int Year, int Month, Guid PlanId), int>> GetPackageDistributionByMonthAsync(
        IReadOnlyList<(int Year, int Month, DateTime Start, DateTime End)> months,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(Guid PlanId, string PlanName, int SubscriptionCount, decimal Revenue)>> GetPackageRevenueAsync(
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(Guid PlanId, string PlanName)>> GetSubscriptionPlansAsync(
        CancellationToken cancellationToken = default);
}
