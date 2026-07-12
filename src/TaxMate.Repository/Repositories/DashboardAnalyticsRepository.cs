using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;
using TaxMate.Model.Data;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class DashboardAnalyticsRepository : IDashboardAnalyticsRepository
{
    private readonly AppDbContext _context;

    public DashboardAnalyticsRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> CountActiveBusinessesAsync(
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken = default)
    {
        return await (
            from business in _context.BusinessProfiles.AsNoTracking()
            where business.IsActive
            where _context.UserSubscriptions.AsNoTracking().Any(subscription =>
                subscription.UserId == business.OwnerId
                && subscription.Status == "Active"
                && subscription.StartDate < periodEnd
                && (subscription.EndDate == null || subscription.EndDate >= periodStart))
            select business.Id
        ).Distinct().CountAsync(cancellationToken);
    }

    public async Task<int> CountPaidSubscriptionsAsync(
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken = default)
    {
        return await _context.UserSubscriptions.AsNoTracking()
            .CountAsync(
                subscription => subscription.CreatedAt >= periodStart && subscription.CreatedAt < periodEnd,
                cancellationToken);
    }

    public async Task<decimal> SumSubscriptionRevenueAsync(
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken = default)
    {
        var subscriptions = await (
            from subscription in _context.UserSubscriptions.AsNoTracking()
            join plan in _context.SubscriptionPlans.AsNoTracking()
                on subscription.SubscriptionPlanId equals plan.Id
            where subscription.CreatedAt >= periodStart && subscription.CreatedAt < periodEnd
            select new
            {
                subscription.BillingCycle,
                plan.MonthlyPrice,
                plan.AnnualPrice
            }
        ).ToListAsync(cancellationToken);

        return subscriptions.Sum(subscription =>
            subscription.BillingCycle.Equals("Annual", StringComparison.OrdinalIgnoreCase)
                ? subscription.AnnualPrice
                : subscription.MonthlyPrice);
    }

    public async Task<Dictionary<(int Year, int Month), int>> GetSubscriptionCountsByMonthAsync(
        IReadOnlyList<(int Year, int Month, DateTime Start, DateTime End)> months,
        CancellationToken cancellationToken = default)
    {
        var earliestStart = months.Min(month => month.Start);
        var latestEnd = months.Max(month => month.End);

        var subscriptions = await _context.UserSubscriptions.AsNoTracking()
            .Where(subscription => subscription.CreatedAt >= earliestStart && subscription.CreatedAt < latestEnd)
            .Select(subscription => subscription.CreatedAt)
            .ToListAsync(cancellationToken);

        return BuildMonthlyCountDictionary(months, subscriptions);
    }

    public async Task<Dictionary<(int Year, int Month), int>> GetDistinctSubscribedUsersByMonthAsync(
        IReadOnlyList<(int Year, int Month, DateTime Start, DateTime End)> months,
        CancellationToken cancellationToken = default)
    {
        var earliestStart = months.Min(month => month.Start);
        var latestEnd = months.Max(month => month.End);

        var subscriptions = await _context.UserSubscriptions.AsNoTracking()
            .Where(subscription => subscription.CreatedAt >= earliestStart && subscription.CreatedAt < latestEnd)
            .Select(subscription => new { subscription.UserId, subscription.CreatedAt })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<(int Year, int Month), int>();

        foreach (var month in months)
        {
            var count = subscriptions
                .Where(subscription =>
                    subscription.CreatedAt >= month.Start
                    && subscription.CreatedAt < month.End)
                .Select(subscription => subscription.UserId)
                .Distinct()
                .Count();

            result[(month.Year, month.Month)] = count;
        }

        return result;
    }

    public async Task<Dictionary<(int Year, int Month, Guid PlanId), int>> GetPackageDistributionByMonthAsync(
        IReadOnlyList<(int Year, int Month, DateTime Start, DateTime End)> months,
        CancellationToken cancellationToken = default)
    {
        var earliestStart = months.Min(month => month.Start);
        var latestEnd = months.Max(month => month.End);

        var subscriptions = await _context.UserSubscriptions.AsNoTracking()
            .Where(subscription => subscription.CreatedAt >= earliestStart && subscription.CreatedAt < latestEnd)
            .Select(subscription => new
            {
                subscription.SubscriptionPlanId,
                subscription.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<(int Year, int Month, Guid PlanId), int>();

        foreach (var month in months)
        {
            var monthSubscriptions = subscriptions
                .Where(subscription =>
                    subscription.CreatedAt >= month.Start
                    && subscription.CreatedAt < month.End);

            foreach (var group in monthSubscriptions.GroupBy(subscription => subscription.SubscriptionPlanId))
            {
                result[(month.Year, month.Month, group.Key)] = group.Count();
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<(Guid PlanId, string PlanName, int SubscriptionCount, decimal Revenue)>> GetPackageRevenueAsync(
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken = default)
    {
        var rows = await (
            from subscription in _context.UserSubscriptions.AsNoTracking()
            join plan in _context.SubscriptionPlans.AsNoTracking()
                on subscription.SubscriptionPlanId equals plan.Id
            where subscription.CreatedAt >= periodStart && subscription.CreatedAt < periodEnd
            group subscription by new { plan.Id, plan.Name, subscription.BillingCycle, plan.MonthlyPrice, plan.AnnualPrice }
            into grouped
            select new
            {
                grouped.Key.Id,
                grouped.Key.Name,
                grouped.Key.BillingCycle,
                grouped.Key.MonthlyPrice,
                grouped.Key.AnnualPrice,
                SubscriptionCount = grouped.Count()
            }
        ).ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => new { row.Id, row.Name })
            .Select(group => (
                PlanId: group.Key.Id,
                PlanName: group.Key.Name,
                SubscriptionCount: group.Sum(row => row.SubscriptionCount),
                Revenue: group.Sum(row =>
                    row.BillingCycle.Equals("Annual", StringComparison.OrdinalIgnoreCase)
                        ? row.AnnualPrice * row.SubscriptionCount
                        : row.MonthlyPrice * row.SubscriptionCount)
            ))
            .OrderByDescending(item => item.Revenue)
            .ToList();
    }

    public async Task<IReadOnlyList<(Guid PlanId, string PlanName)>> GetSubscriptionPlansAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.SubscriptionPlans.AsNoTracking()
            .OrderBy(plan => plan.SortOrder)
            .Select(plan => new ValueTuple<Guid, string>(plan.Id, plan.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAssistantChatMessagesAsync(
        DateTime? periodStart,
        DateTime? periodEnd,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ChatMessages.AsNoTracking()
            .Where(message => message.Role == ChatMessageRole.Assistant);

        if (periodStart.HasValue)
        {
            query = query.Where(message => message.CreatedAt >= periodStart.Value);
        }

        if (periodEnd.HasValue)
        {
            query = query.Where(message => message.CreatedAt < periodEnd.Value);
        }

        return await query.CountAsync(cancellationToken);
    }

    public async Task<double?> GetAverageSimilarityScoreAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ChatReferences.AsNoTracking()
            .AverageAsync(reference => (double?)reference.SimilarityScore, cancellationToken);
    }

    public async Task<int> CountOwnerUsersAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users.AsNoTracking()
            .CountAsync(user => user.Role == UserRoles.Owner, cancellationToken);
    }

    public async Task<IReadOnlyList<(Guid PlanId, string PlanName, int UserCount)>> GetActiveUsersByPlanAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await (
            from subscription in _context.UserSubscriptions.AsNoTracking()
            join plan in _context.SubscriptionPlans.AsNoTracking()
                on subscription.SubscriptionPlanId equals plan.Id
            join user in _context.Users.AsNoTracking()
                on subscription.UserId equals user.Id
            where subscription.Status == "Active"
            where user.Role == UserRoles.Owner
            select new
            {
                plan.Id,
                plan.Name,
                plan.SortOrder,
                subscription.UserId
            }
        ).ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => new { row.Id, row.Name, row.SortOrder })
            .OrderBy(group => group.Key.SortOrder)
            .Select(group => (
                PlanId: group.Key.Id,
                PlanName: group.Key.Name,
                UserCount: group.Select(row => row.UserId).Distinct().Count()))
            .ToList();
    }

    private static Dictionary<(int Year, int Month), int> BuildMonthlyCountDictionary(
        IReadOnlyList<(int Year, int Month, DateTime Start, DateTime End)> months,
        IReadOnlyList<DateTime> timestamps)
    {
        var result = new Dictionary<(int Year, int Month), int>();

        foreach (var month in months)
        {
            var count = timestamps.Count(timestamp =>
                timestamp >= month.Start
                && timestamp < month.End);

            result[(month.Year, month.Month)] = count;
        }

        return result;
    }
}
