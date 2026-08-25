using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public sealed class InventoryPurchaseRepository : IInventoryPurchaseRepository
{
    private readonly AppDbContext _context;

    public InventoryPurchaseRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Guid?> GetBusinessOwnerIdAsync(
        Guid businessId,
        CancellationToken cancellationToken = default)
        => _context.BusinessProfiles
            .AsNoTracking()
            .Where(x => x.Id == businessId)
            .Select(x => (Guid?)x.OwnerId)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Expense?> GetExpenseForWriteAsync(
        Guid expenseId,
        CancellationToken cancellationToken = default)
        => ExpenseDocumentQuery(tracking: true)
            .SingleOrDefaultAsync(x => x.ExpenseId == expenseId, cancellationToken);

    public Task<Expense?> GetExpenseForReadAsync(
        Guid expenseId,
        CancellationToken cancellationToken = default)
        => ExpenseDocumentQuery(tracking: false)
            .SingleOrDefaultAsync(x => x.ExpenseId == expenseId, cancellationToken);

    public async Task<(IReadOnlyList<Expense> Items, int TotalCount)> GetPagedAsync(
        Guid businessId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var expenseIds = _context.InventoryMovements
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.MovementType == InventoryMovementTypes.PurchaseIn &&
                x.ReferenceId.HasValue)
            .Select(x => x.ReferenceId!.Value)
            .Distinct();
        var query = ExpenseDocumentQuery(tracking: false)
            .Where(x => x.BusinessId == businessId && expenseIds.Contains(x.ExpenseId));
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.ExpenseDate)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task<IReadOnlyList<InventoryMovement>> GetSourceMovementsAsync(
        Guid businessId,
        Guid expenseId,
        CancellationToken cancellationToken = default)
        => await _context.InventoryMovements
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Ingredient)
            .Where(x =>
                x.BusinessId == businessId &&
                x.MovementType == InventoryMovementTypes.PurchaseIn &&
                x.ReferenceId == expenseId)
            .OrderBy(x => x.ProductId)
            .ThenBy(x => x.IngredientId)
            .ToListAsync(cancellationToken);

    public Task<MoneyMovement?> GetExpenseMoneyMovementAsync(
        Guid expenseId,
        CancellationToken cancellationToken = default)
        => _context.MoneyMovements
            .AsNoTracking()
            .Include(x => x.PaymentAccount)
            .SingleOrDefaultAsync(
                x =>
                    x.MovementType == MoneyMovementTypes.ExpenseOut &&
                    x.ReferenceId == expenseId,
                cancellationToken);

    public Task<ExpenseCategory?> GetExpenseCategoryAsync(
        Guid expenseCategoryId,
        CancellationToken cancellationToken = default)
        => _context.ExpenseCategories
            .SingleOrDefaultAsync(
                x => x.ExpenseCategoryId == expenseCategoryId,
                cancellationToken);

    public Task<Supplier?> GetSupplierAsync(
        Guid supplierId,
        CancellationToken cancellationToken = default)
        => _context.Suppliers
            .SingleOrDefaultAsync(x => x.Id == supplierId, cancellationToken);

    public async Task<IReadOnlyList<Product>> GetProductsForWriteAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default)
        => productIds.Count == 0
            ? []
            : await _context.Products
                .IgnoreQueryFilters()
                .Where(x => productIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Ingredient>> GetIngredientsForWriteAsync(
        IReadOnlyCollection<Guid> ingredientIds,
        CancellationToken cancellationToken = default)
        => ingredientIds.Count == 0
            ? []
            : await _context.Ingredients
                .IgnoreQueryFilters()
                .Where(x => ingredientIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<InventoryMovement>> GetEffectiveLedgerForCacheAsync(
        Guid businessId,
        IReadOnlyCollection<Guid> productIds,
        IReadOnlyCollection<Guid> ingredientIds,
        CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0 && ingredientIds.Count == 0)
        {
            return [];
        }

        var persisted = await _context.InventoryMovements
            .IgnoreQueryFilters()
            .Where(x =>
                x.BusinessId == businessId &&
                ((x.ProductId.HasValue && productIds.Contains(x.ProductId.Value)) ||
                 (x.IngredientId.HasValue && ingredientIds.Contains(x.IngredientId.Value))))
            .ToListAsync(cancellationToken);
        var effective = persisted.ToDictionary(x => x.InventoryMovementId);

        foreach (var entry in _context.ChangeTracker.Entries<InventoryMovement>())
        {
            var movement = entry.Entity;
            if (movement.BusinessId != businessId ||
                !((movement.ProductId.HasValue && productIds.Contains(movement.ProductId.Value)) ||
                  (movement.IngredientId.HasValue && ingredientIds.Contains(movement.IngredientId.Value))))
            {
                continue;
            }

            if (entry.State == EntityState.Deleted)
            {
                effective.Remove(movement.InventoryMovementId);
            }
            else if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Unchanged)
            {
                effective[movement.InventoryMovementId] = movement;
            }
        }

        return effective.Values
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.InventoryMovementId)
            .ToList();
    }

    private IQueryable<Expense> ExpenseDocumentQuery(bool tracking)
    {
        var query = _context.Expenses
            .Include(x => x.ExpenseCategory)
            .Include(x => x.Supplier)
            .Include(x => x.IngredientPurchases)
                .ThenInclude(x => x.Ingredient)
            .AsSplitQuery();
        return tracking ? query : query.AsNoTracking();
    }
}
