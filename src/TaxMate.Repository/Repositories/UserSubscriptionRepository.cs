using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class UserSubscriptionRepository : GenericRepository<UserSubscription>, IUserSubscriptionRepository
{
    private readonly AppDbContext _appContext;

    public UserSubscriptionRepository(AppDbContext context) : base(context)
    {
        _appContext = context;
    }

    public async Task<UserSubscription?> GetActiveByUserIdAsync(Guid userId)
    {
        return await _appContext.UserSubscriptions
            .Include(x => x.SubscriptionPlan)
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Status == "Active");
    }

    public async Task<UserSubscription?> GetByOrderCodeAsync(long orderCode)
    {
        return await _appContext.UserSubscriptions
            .Include(x => x.SubscriptionPlan)
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.PaymentOrderCode == orderCode);
    }

    public async Task<List<UserSubscription>> GetAllByUserIdWithDetailsAsync(Guid userId)
    {
        return await _appContext.UserSubscriptions
            .Include(x => x.SubscriptionPlan)
            .Include(x => x.User)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }
}
