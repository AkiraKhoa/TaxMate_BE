using TaxMate.Model.DTO.TaxPeriod;

namespace TaxMate.Service.Interfaces;

public interface ITknTaxPeriodService
{
    Task<TknTaxPeriodPreviewResponse> GetPreviewAsync(Guid userId, Guid taxPeriodId, CancellationToken cancellationToken = default);
    Task<CloseTknTaxPeriodResponse> CloseAsync(Guid userId, Guid taxPeriodId, CloseTknTaxPeriodRequest request, CancellationToken cancellationToken = default);
    Task<TknTaxCalculationResponse> CalculateAsync(Guid userId, Guid taxPeriodId, CancellationToken cancellationToken = default);

    Task<TknQttNextStepResponse> GetQttNextStepAsync(
        Guid userId,
        Guid taxPeriodId,
        CancellationToken cancellationToken = default);

    Task<TknQttNextStepResponse> ApplyQttNextStepAsync(
        Guid userId,
        Guid taxPeriodId,
        ApplyTknQttNextStepRequest request,
        CancellationToken cancellationToken = default);
}
