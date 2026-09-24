using TaxMate.Model.DTO.MoneyMovement;

namespace TaxMate.Service.Interfaces;

/// <summary>
/// Internal application primitive for staging money-ledger changes in the
/// caller's DbContext. The source coordinator must validate that ReferenceId
/// identifies the expected Payment, manual Income, or Expense and that source
/// belongs to the same owner/business before calling this writer. The source
/// coordinator also owns the transaction and the single SaveChanges call.
/// This interface must not be exposed as a client-controlled movement API.
/// </summary>
public interface IMoneyMovementService
{
    Task<MoneyMovementWriteResult> SyncAsync(
        MoneyMovementWriteRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid ownerId,
        Guid businessId,
        string movementType,
        Guid referenceId,
        CancellationToken cancellationToken = default);
}
