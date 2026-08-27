using TaxMate.Model.DTO.Inventory;

namespace TaxMate.Service.Interfaces;

public interface IInventoryInitializationService
{
    Task<InventoryInitializationPreviewResponse> GetPreviewAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        CancellationToken cancellationToken = default);

    Task<InventoryControlResultResponse> InitializeAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        InitializeInventoryRequest request,
        CancellationToken cancellationToken = default);
}
