using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IInventoryPurchaseRepository
{
    Task<Guid?> GetBusinessOwnerIdAsync(
        Guid businessId,
        CancellationToken cancellationToken = default);

    Task<Expense?> GetExpenseForWriteAsync(
        Guid expenseId,
        CancellationToken cancellationToken = default);

    Task<Expense?> GetExpenseForReadAsync(
        Guid expenseId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Expense> Items, int TotalCount)> GetPagedAsync(
        Guid businessId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryMovement>> GetSourceMovementsAsync(
        Guid businessId,
        Guid expenseId,
        CancellationToken cancellationToken = default);

    Task<MoneyMovement?> GetExpenseMoneyMovementAsync(
        Guid expenseId,
        CancellationToken cancellationToken = default);

    Task<ExpenseCategory?> GetExpenseCategoryAsync(
        Guid expenseCategoryId,
        CancellationToken cancellationToken = default);

    Task<Supplier?> GetSupplierAsync(
        Guid supplierId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetProductsForWriteAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Ingredient>> GetIngredientsForWriteAsync(
        IReadOnlyCollection<Guid> ingredientIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the effective ledger after overlaying the current DbContext's
    /// added/modified/deleted entries. This lets a source coordinator rebuild
    /// caches before its one and only SaveChanges call.
    /// </summary>
    Task<IReadOnlyList<InventoryMovement>> GetEffectiveLedgerForCacheAsync(
        Guid businessId,
        IReadOnlyCollection<Guid> productIds,
        IReadOnlyCollection<Guid> ingredientIds,
        CancellationToken cancellationToken = default);
}
