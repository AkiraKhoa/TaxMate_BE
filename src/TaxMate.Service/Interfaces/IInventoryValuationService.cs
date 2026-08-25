using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.Entities;
using TaxMate.Service.Services;

namespace TaxMate.Service.Interfaces;

internal interface IInventoryValuationService
{
    InventoryPeriodValuation PreviewQuarter(
        IReadOnlyCollection<InventoryMovement> movementsBeforePeriodEnd,
        int year,
        int quarter);

    /// <summary>
    /// Applies whole-quarter average values to outbound movements in exactly one
    /// Bangkok S2d quarter. It never saves changes.
    /// </summary>
    InventoryPeriodValuation StageFinalizeQuarter(
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

    /// <summary>
    /// Annual QTT bridge: the coordinator must prove all four S2d quarters are
    /// closed. This method then sums stored outbound values and never
    /// recalculates or mutates quarterly movements.
    /// </summary>
    decimal AggregateFinalizedCalendarYearOutboundValue(
        IReadOnlyCollection<InventoryMovement> finalizedMovements,
        int year,
        InventoryAnnualClosureEvidence closureEvidence);
}
