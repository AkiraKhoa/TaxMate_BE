using TaxMate.Model.Common;
using TaxMate.Model.DTO.InventoryPurchase;

namespace TaxMate.Service.Interfaces;

public interface IInventoryPurchaseService
{
    Task<InventoryPurchaseResponse> CreateAsync(
        Guid ownerId,
        Guid businessId,
        CreateInventoryPurchaseRequest request,
        CancellationToken cancellationToken = default);

    Task<InventoryPurchaseResponse> UpdateAsync(
        Guid ownerId,
        Guid expenseId,
        UpdateInventoryPurchaseRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid ownerId,
        Guid expenseId,
        CancellationToken cancellationToken = default);

    Task<InventoryPurchaseResponse> GetByIdAsync(
        Guid ownerId,
        Guid expenseId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<InventoryPurchaseResponse>> GetPagedAsync(
        Guid ownerId,
        Guid businessId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
