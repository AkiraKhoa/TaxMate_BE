using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface ISubscriptionPlanRepository : IGenericRepository<SubscriptionPlan>
{
    Task<bool> ExistsByNameAsync(string name);

    Task<List<SubscriptionPlan>> GetActivePlansWithFeaturesAsync();
    Task<SubscriptionPlan?> GetByIdWithFeaturesAsync(Guid id);

    Task<List<SubscriptionPlan>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        bool? isActive = null);

    Task<int> CountAsync(bool? isActive = null);
    
}
