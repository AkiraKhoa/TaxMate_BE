using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface ISubscriptionPlanRepository : IGenericRepository<SubscriptionPlan>
{
    Task<List<SubscriptionPlan>> GetActivePlansWithFeaturesAsync();

    Task<List<SubscriptionPlan>> GetAllPlansWithFeaturesAsync();

    Task<SubscriptionPlan?> GetByIdWithFeaturesAsync(Guid id);

    Task<bool> HasAnySubscriptionsAsync(Guid planId);
}
