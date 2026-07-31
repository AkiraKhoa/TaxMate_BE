using TaxMate.Model.DTO.SubscriptionPlan;
using SubscriptionPlanResponse = TaxMate.Model.DTO.SubscriptionPlanResponse;

namespace TaxMate.Service.Interfaces;

public interface ISubscriptionPlanAdminService
{
    Task<IEnumerable<SubscriptionPlanResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<SubscriptionPlanResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SubscriptionPlanResponse> CreateAsync(
        CreateSubscriptionPlanRequest request,
        CancellationToken cancellationToken = default);

    Task<SubscriptionPlanResponse> UpdateAsync(
        Guid id,
        UpdateSubscriptionPlanRequest request,
        CancellationToken cancellationToken = default);

    Task<SubscriptionPlanResponse> ToggleActiveAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
