using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IUserSubscriptionRepository : IGenericRepository<UserSubscription>
{
    Task<UserSubscription?> GetActiveByUserIdAsync(Guid userId);

    Task<UserSubscription?> GetByOrderCodeAsync(long orderCode);

    Task<List<UserSubscription>> GetAllByUserIdWithDetailsAsync(Guid userId);
}
