using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface ISubscriptionPlanRepository : IGenericRepository<SubscriptionPlan>
{
    Task<List<SubscriptionPlan>> GetActivePlansWithFeaturesAsync();
    Task<SubscriptionPlan?> GetByIdWithFeaturesAsync(Guid id);
}
