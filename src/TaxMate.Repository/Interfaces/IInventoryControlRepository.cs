using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IInventoryControlRepository
{
    Task<BusinessProfile?> GetBusinessAsync(
        Guid businessId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetActiveProductsAsync(
        Guid businessId,
        bool tracking,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Ingredient>> GetActiveIngredientsAsync(
        Guid businessId,
        bool tracking,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryMovement>> GetMovementsAsync(
        Guid businessId,
        bool tracking,
        CancellationToken cancellationToken = default);

    Task<bool> HasMovementsForProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<bool> HasMovementsForIngredientAsync(
        Guid ingredientId,
        CancellationToken cancellationToken = default);
}
