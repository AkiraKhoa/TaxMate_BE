using TaxMate.Model.DTO.Inventory;

namespace TaxMate.Service.Interfaces;

/// <summary>
/// Boundary contract implemented by the source coordinator, not by the ledger.
/// It must validate both target existence and BusinessId ownership before the
/// writer stages a referenced movement:
/// PurchaseIn.ReferenceId -> Expense.Id;
/// OrderOut.ReferenceId -> Transaction.Id.
/// OpeningBalance/AdjustmentIn/AdjustmentOut never have ReferenceId and do not
/// pass through this contract. Validation and staging must run in the caller's
/// same database transaction, before the source target can be deleted.
/// </summary>
internal interface IInventoryMovementCoordinatorValidator
{
    Task EnsureValidReferenceTargetAsync(
        InventoryMovementReferenceTarget target,
        CancellationToken cancellationToken = default);
}
