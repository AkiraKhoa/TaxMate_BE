using TaxMate.Model.Common;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.Entities;
using TaxMate.Service.Common;

namespace TaxMate.Service.Services;

internal static class InventoryLedgerCacheProjector
{
    public static IReadOnlyDictionary<InventoryItemKey, InventoryCacheSnapshot> Project(
        IEnumerable<InventoryMovement> movements)
    {
        ArgumentNullException.ThrowIfNull(movements);
        return movements
            .GroupBy(ToKey)
            .ToDictionary(group => group.Key, ProjectItem);
    }

    private static InventoryCacheSnapshot ProjectItem(
        IGrouping<InventoryItemKey, InventoryMovement> group)
    {
        decimal quantity = 0m;
        decimal value = 0m;
        var valuationComplete = true;
        var quarters = group
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.InventoryMovementId)
            .GroupBy(QuarterKey)
            .OrderBy(x => x.Key.Year)
            .ThenBy(x => x.Key.Quarter);

        foreach (var quarter in quarters)
        {
            var inbound = quarter.Where(x => IsInbound(x.MovementType)).ToList();
            var outbound = quarter.Where(x => IsOutbound(x.MovementType)).ToList();
            if (inbound.Count + outbound.Count != quarter.Count())
            {
                throw new InvalidOperationException("Unsupported inventory movement type in cache projection.");
            }

            var inboundQuantity = inbound.Sum(x => x.Quantity);
            var inboundValue = 0m;
            foreach (var movement in inbound)
            {
                if (!movement.TotalValue.HasValue)
                {
                    valuationComplete = false;
                    continue;
                }

                inboundValue += movement.TotalValue.Value;
            }

            var valuationQuantity = quantity + inboundQuantity;
            var valuationValue = value + inboundValue;
            decimal? provisionalAverage = null;
            if (valuationComplete && valuationQuantity > 0m && valuationValue >= 0m)
            {
                provisionalAverage = valuationValue / valuationQuantity;
            }

            var outboundValue = 0m;
            foreach (var movement in outbound)
            {
                if (movement.TotalValue.HasValue)
                {
                    outboundValue += movement.TotalValue.Value;
                }
                else if (provisionalAverage.HasValue)
                {
                    outboundValue += provisionalAverage.Value * movement.Quantity;
                }
                else
                {
                    valuationComplete = false;
                }
            }

            quantity = valuationQuantity - outbound.Sum(x => x.Quantity);
            value = quantity == 0m
                ? 0m
                : Math.Round(
                    valuationValue - outboundValue,
                    2,
                    MidpointRounding.AwayFromZero);
        }

        decimal? unitValue = valuationComplete && quantity > 0m && value >= 0m
            ? Math.Round(value / quantity, 6, MidpointRounding.AwayFromZero)
            : null;
        return new InventoryCacheSnapshot(quantity, unitValue);
    }

    private static InventoryItemKey ToKey(InventoryMovement movement)
    {
        if (movement.ProductId.HasValue == movement.IngredientId.HasValue)
        {
            throw new InvalidOperationException(
                "Inventory movement must reference exactly one product or ingredient.");
        }

        return new InventoryItemKey(movement.ProductId, movement.IngredientId);
    }

    private static (int Year, int Quarter) QuarterKey(InventoryMovement movement)
    {
        var wallClock = BangkokBusinessTime.NaiveUtcToBangkokWallClock(
            DateTime.SpecifyKind(movement.OccurredAt, DateTimeKind.Unspecified));
        return (wallClock.Year, ((wallClock.Month - 1) / 3) + 1);
    }

    private static bool IsInbound(string movementType) =>
        movementType is
            InventoryMovementTypes.OpeningBalance or
            InventoryMovementTypes.PurchaseIn or
            InventoryMovementTypes.AdjustmentIn;

    private static bool IsOutbound(string movementType) =>
        movementType is
            InventoryMovementTypes.OrderOut or
            InventoryMovementTypes.AdjustmentOut;
}

internal sealed record InventoryCacheSnapshot(
    decimal Quantity,
    decimal? UnitValue);
