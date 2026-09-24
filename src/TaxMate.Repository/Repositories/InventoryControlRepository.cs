using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public sealed class InventoryControlRepository : IInventoryControlRepository
{
    public Task UseSerializableTransactionAsync(CancellationToken cancellationToken = default)
        => _dbContext.Database.ExecuteSqlRawAsync("SET TRANSACTION ISOLATION LEVEL SERIALIZABLE", cancellationToken);
    private readonly AppDbContext _dbContext;

    public InventoryControlRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<BusinessProfile?> GetBusinessAsync(
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.BusinessProfiles
            .FirstOrDefaultAsync(x => x.Id == businessId, cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetActiveProductsAsync(
        Guid businessId,
        bool tracking,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Products
            .Where(x => x.BusinessId == businessId && !x.IsDeleted);
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Ingredient>> GetActiveIngredientsAsync(
        Guid businessId,
        bool tracking,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Ingredients
            .Where(x => x.BusinessId == businessId && !x.IsDeleted);
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryMovement>> GetMovementsAsync(
        Guid businessId,
        bool tracking,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.InventoryMovements
            .Where(x => x.BusinessId == businessId);
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.InventoryMovementId)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasMovementsForProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.InventoryMovements
            .AnyAsync(x => x.ProductId == productId, cancellationToken);
    }

    public Task<bool> HasMovementsForIngredientAsync(
        Guid ingredientId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.InventoryMovements
            .AnyAsync(x => x.IngredientId == ingredientId, cancellationToken);
    }
}
