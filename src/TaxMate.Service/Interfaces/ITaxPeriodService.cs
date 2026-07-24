using TaxMate.Model.DTO.TaxPeriod;

namespace TaxMate.Service.Interfaces;

public interface ITaxPeriodService
{
    Task<IReadOnlyList<TaxPeriodSummaryResponse>> GetByBusinessAsync(
        Guid userId,
        Guid businessId,
        GetTaxPeriodsRequest request,
        CancellationToken cancellationToken = default);

    Task<TaxPeriodDetailResponse> GetByIdAsync(
        Guid userId,
        Guid taxPeriodId,
        CancellationToken cancellationToken = default);
    
    Task<TaxPeriodPreviewResponse> GetPreviewAsync(
        Guid userId,
        Guid taxPeriodId,
        CancellationToken cancellationToken = default);

    Task<CloseTaxPeriodResponse> CloseAsync(
        Guid userId,
        Guid taxPeriodId,
        CloseTaxPeriodRequest request,
        CancellationToken cancellationToken = default);

    Task<TaxCalculationResponse> CalculateAsync(
        Guid userId,
        Guid taxPeriodId,
        CancellationToken cancellationToken = default);

}