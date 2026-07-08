using TaxMate.Model.Common;
using TaxMate.Model.DTO.SubscriptionPlan;

namespace TaxMate.Service.Interfaces;

public interface ISubscriptionPlanService
{
    Task<PagedResult<SubscriptionPlanResponse>> GetPagedAsync(
        int page,
        int pageSize,
        bool? isActive);

    Task<Guid> CreateAsync(
        CreateSubscriptionPlanRequest request);
    
    Task<SubscriptionPlanResponse> GetByIdAsync(Guid id);

    Task UpdateAsync(Guid id, UpdateSubscriptionPlanRequest request);

    Task DeactivateAsync(Guid id);

    Task ActivateAsync(Guid id);
}