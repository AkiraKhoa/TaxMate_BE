using TaxMate.Model.DTO.TaxPolicy;

namespace TaxMate.Service.Interfaces;

public interface ITaxPolicyService
{
    Task<TaxThresholdSettingResponse> GetEffectiveThresholdAsync(
        string type,
        DateOnly effectiveOn,
        CancellationToken cancellationToken = default);

    Task<TaxThresholdSettingResponse> GetLatestThresholdAsync(
        string type,
        CancellationToken cancellationToken = default);

    Task<EffectiveTaxPolicyResponse> GetEffectiveAsync(
        DateOnly effectiveOn,
        CancellationToken cancellationToken = default);

    Task<TaxThresholdSettingResponse> UpsertAsync(
        string type,
        UpdateTaxThresholdSettingRequest request,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default);
}
