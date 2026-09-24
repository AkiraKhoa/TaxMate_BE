using TaxMate.Model.DTO.Inventory;

namespace TaxMate.Service.Interfaces;

public interface IInventoryAdjustmentService
{
    Task<InventoryControlResultResponse> ReconcileAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        ReconcileInventoryRequest request,
        bool enableStockTracking,
        CancellationToken cancellationToken = default);
}
