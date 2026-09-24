using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class InventoryMovementRepository
    : GenericRepository<InventoryMovement>, IInventoryMovementRepository
{
    private readonly AppDbContext _dbContext;

    public InventoryMovementRepository(AppDbContext dbContext)
        : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<InventoryMovement>> GetBySourceForUpdateAsync(
        Guid businessId,
        string movementType,
        Guid referenceId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.InventoryMovements
            .Where(x =>
                x.BusinessId == businessId &&
                x.MovementType == movementType &&
                x.ReferenceId == referenceId)
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.InventoryMovementId)
            .ToListAsync(cancellationToken);
    }

    public Task<InventoryMovement?> GetOpeningForUpdateAsync(
        Guid businessId,
        Guid? productId,
        Guid? ingredientId,
        CancellationToken cancellationToken = default)
    {
        if (productId.HasValue == ingredientId.HasValue)
        {
            throw new ArgumentException(
                "Exactly one of productId and ingredientId must be supplied.");
        }

        return _dbContext.InventoryMovements
            .FirstOrDefaultAsync(
                x =>
                    x.BusinessId == businessId &&
                    x.MovementType == TaxMate.Model.Common.InventoryMovementTypes.OpeningBalance &&
                    (productId.HasValue
                        ? x.ProductId == productId
                        : x.IngredientId == ingredientId),
                cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryMovement>> GetBeforeAsync(
        Guid businessId,
        DateTime endExclusive,
        CancellationToken cancellationToken = default)
    {
        return await BookQuery()
            .Where(x =>
                x.BusinessId == businessId &&
                x.OccurredAt < endExclusive)
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.InventoryMovementId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryMovement>> GetBeforeForUpdateAsync(
        Guid businessId,
        DateTime endExclusive,
        CancellationToken cancellationToken = default)
    {
        return await BookQuery(tracking: true)
            .Where(x =>
                x.BusinessId == businessId &&
                x.OccurredAt < endExclusive)
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.InventoryMovementId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetProductsIncludingDeletedAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
        {
            return Array.Empty<Product>();
        }

        return await _dbContext.Products
            .IgnoreQueryFilters()
            .Where(x => productIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Ingredient>> GetIngredientsIncludingDeletedAsync(
        IReadOnlyCollection<Guid> ingredientIds,
        CancellationToken cancellationToken = default)
    {
        if (ingredientIds.Count == 0)
        {
            return Array.Empty<Ingredient>();
        }

        return await _dbContext.Ingredients
            .IgnoreQueryFilters()
            .Where(x => ingredientIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<InventoryMovement> BookQuery()
    {
        // IgnoreQueryFilters is intentional: a deactivated item remains part of
        // every historical S2d book in which it has an opening or movement.
        return BookQuery(tracking: false);
    }

    private IQueryable<InventoryMovement> BookQuery(bool tracking)
    {
        var query = _dbContext.InventoryMovements
            .IgnoreQueryFilters()
            .Include(x => x.Product)
            .Include(x => x.Ingredient);
        return tracking ? query : query.AsNoTracking();
    }
}
