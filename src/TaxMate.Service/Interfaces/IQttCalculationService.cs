using TaxMate.Model.DTO.Tax;

namespace TaxMate.Service.Interfaces;

public interface IQttCalculationService
{
    Task<QttCalculationResponse> CalculateAsync(
        Guid userId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default);
}
