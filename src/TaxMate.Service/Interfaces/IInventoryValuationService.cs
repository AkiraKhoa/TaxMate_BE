using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.Entities;

namespace TaxMate.Service.Interfaces;

internal interface IInventoryValuationService
{
    InventoryPeriodValuation PreviewQuarter(
        IReadOnlyCollection<InventoryMovement> movementsBeforePeriodEnd,
        int year,
        int quarter);

    /// <summary>
    /// Compatibility boundary for a period coordinator. Only an exact Bangkok
    /// quarter [start, end) in naive-UTC encoding is accepted. Calendar-year,
    /// month and arbitrary windows are rejected.
    /// </summary>
    InventoryPeriodValuation StageFinalizeBookPeriod(
        IReadOnlyCollection<InventoryMovement> movementsBeforePeriodEnd,
        DateTime periodStartNaiveUtc,
        DateTime periodEndExclusiveNaiveUtc);

}

public interface IInventoryQuarterFinalizer
{
    InventoryPeriodValuation StageFinalizeBookPeriod(
        IReadOnlyCollection<InventoryMovement> movementsBeforePeriodEnd,
        DateTime periodStartNaiveUtc,
        DateTime periodEndExclusiveNaiveUtc);
}
