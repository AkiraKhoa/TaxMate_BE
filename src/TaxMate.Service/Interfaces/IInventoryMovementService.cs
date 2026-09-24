using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.Entities;

namespace TaxMate.Service.Interfaces;

public interface IInventoryMovementService
{
    /// <summary>
    /// Stages a full source replacement in the caller's DbContext transaction.
    /// Duplicate source lines for the same item are aggregated before staging.
    /// This method never saves or opens a transaction.
    /// </summary>
    Task<IReadOnlyList<InventoryMovement>> StageReplaceSourceAsync(
        ReplaceInventorySourceMovementsCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages removal of all movements owned by one source. The caller owns
    /// the source mutation, cache rebuild, SaveChanges and transaction.
    /// </summary>
    Task StageRemoveSourceAsync(
        Guid businessId,
        string movementType,
        Guid referenceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages the one-time cutover openings. Existing openings for the same
    /// item are updated, not duplicated. This method never saves.
    /// </summary>
    Task<IReadOnlyList<InventoryMovement>> StageOpeningBalancesAsync(
        StageInventoryOpeningBalancesCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a stocktake delta. AdjustmentIn may be staged without value for
    /// preview, but it becomes a close-period blocker until valued.
    /// </summary>
    Task<InventoryMovement> StageAdjustmentAsync(
        StageInventoryAdjustmentCommand command,
        CancellationToken cancellationToken = default);
}
