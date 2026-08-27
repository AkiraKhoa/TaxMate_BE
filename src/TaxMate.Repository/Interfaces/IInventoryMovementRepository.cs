using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IInventoryMovementRepository
    : IGenericRepository<InventoryMovement>
{
    Task<IReadOnlyList<InventoryMovement>> GetBySourceForUpdateAsync(
        Guid businessId,
        string movementType,
        Guid referenceId,
        CancellationToken cancellationToken = default);

    Task<InventoryMovement?> GetOpeningForUpdateAsync(
        Guid businessId,
        Guid? productId,
        Guid? ingredientId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryMovement>> GetBeforeAsync(
        Guid businessId,
        DateTime endExclusive,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryMovement>> GetBeforeForUpdateAsync(
        Guid businessId,
        DateTime endExclusive,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetProductsIncludingDeletedAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Ingredient>> GetIngredientsIncludingDeletedAsync(
        IReadOnlyCollection<Guid> ingredientIds,
        CancellationToken cancellationToken = default);
}
