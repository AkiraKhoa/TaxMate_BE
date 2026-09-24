namespace TaxMate.Repository.Interfaces;

/// <summary>
/// Transaction-scoped serialization shared by accounting mutations and tax
/// period close. The caller owns begin/commit/rollback and SaveChanges.
/// </summary>
public interface IAccountingTransactionLockRepository
{
    bool HasActiveTransaction { get; }

    /// <summary>
    /// Identifies the active EF transaction. Capabilities issued from reads
    /// under an advisory lock use this value to prevent reuse after commit or
    /// in another transaction.
    /// </summary>
    Guid? CurrentTransactionId { get; }

    /// <summary>
    /// Acquires PostgreSQL transaction advisory locks for one owner and the
    /// supplied calendar years. Implementations acquire distinct years in
    /// ascending order. Tax-period close must use this same lock before its
    /// status transition.
    /// </summary>
    Task AcquireOwnerYearLocksAsync(
        Guid ownerId,
        IReadOnlyCollection<int> years,
        CancellationToken cancellationToken = default);
}
