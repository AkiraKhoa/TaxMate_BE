using TaxMate.Model.DTO.Tax;

namespace TaxMate.Service.Interfaces;

public interface IAnnualTaxAggregateService
{
    Task<QttPreviewResponse> PreviewAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default);
}
