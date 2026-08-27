using TaxMate.Model.Common;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.Entities;
using TaxMate.Service.Common;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

internal sealed class InventoryValuationService
    : IInventoryValuationService, IInventoryQuarterFinalizer
{
    private const int UnitValueScale = 6;
    private const int TotalValueScale = 2;
    private const decimal OneCent = 0.01m;
    public InventoryPeriodValuation PreviewQuarter(
        IReadOnlyCollection<InventoryMovement> movementsBeforePeriodEnd,
        int year,
        int quarter)
    {
        var (start, end) = BangkokBusinessTime.GetQuarterNaiveUtc(year, quarter);
        return CalculateQuarter(movementsBeforePeriodEnd, start, end);
    }

    public InventoryPeriodValuation StageFinalizeBookPeriod(
        IReadOnlyCollection<InventoryMovement> movementsBeforePeriodEnd,
        DateTime periodStartNaiveUtc,
        DateTime periodEndExclusiveNaiveUtc)
    {
        EnsureExactBangkokQuarter(
            periodStartNaiveUtc,
            periodEndExclusiveNaiveUtc);
        return StageFinalizeExactQuarter(
            movementsBeforePeriodEnd,
            periodStartNaiveUtc,
            periodEndExclusiveNaiveUtc);
    }

    private static InventoryPeriodValuation StageFinalizeExactQuarter(
        IReadOnlyCollection<InventoryMovement> movementsBeforePeriodEnd,
        DateTime periodStart,
        DateTime periodEndExclusive)
    {
        var result = CalculateQuarter(
            movementsBeforePeriodEnd,
            periodStart,
            periodEndExclusive);
        if (!result.CanFinalize)
        {
            return result;
        }

        var valuesByMovementId = result.Items
            .SelectMany(x => x.OutboundValuations)
            .ToDictionary(x => x.InventoryMovementId, x => x.TotalValue);
        var currentOutbound = movementsBeforePeriodEnd
            .Where(x =>
                OccurredAt(x) >= periodStart &&
                OccurredAt(x) < periodEndExclusive &&
                IsOutbound(x.MovementType))
            .ToList();
        var conflicts = currentOutbound
            .Where(x =>
                x.TotalValue.HasValue &&
                valuesByMovementId.TryGetValue(
                    x.InventoryMovementId,
                    out var expected) &&
                x.TotalValue.Value != expected)
            .Select(x => Blocker(
                InventoryBookBlockerCodes.ConflictingFinalizedOutboundValue,
                "Dòng xuất đã có giá trị khác kết quả của quý; hệ thống không tự ghi đè dữ liệu đã chốt.",
                ToKey(x),
                x.InventoryMovementId))
            .ToList();
        if (conflicts.Count > 0)
        {
            result.Blockers = result.Blockers.Concat(conflicts).ToList();
            return result;
        }

        foreach (var movement in currentOutbound.Where(x => !x.TotalValue.HasValue))
        {
            if (valuesByMovementId.TryGetValue(
                    movement.InventoryMovementId,
                    out var totalValue))
            {
                movement.TotalValue = totalValue;
            }
        }

        result.IsProvisional = false;
        return result;
    }

    private static InventoryPeriodValuation CalculateQuarter(
        IReadOnlyCollection<InventoryMovement> movementsBeforePeriodEnd,
        DateTime periodStart,
        DateTime periodEndExclusive)
    {
        ArgumentNullException.ThrowIfNull(movementsBeforePeriodEnd);
        BangkokBusinessTime.RequireNaiveUtc(periodStart, nameof(periodStart));
        BangkokBusinessTime.RequireNaiveUtc(
            periodEndExclusive,
            nameof(periodEndExclusive));

        var blockers = ValidateShapes(movementsBeforePeriodEnd);
        var validMovements = movementsBeforePeriodEnd
            .Where(HasValidItem)
            .Where(x => OccurredAt(x) < periodEndExclusive)
            .ToList();
        var duplicateSourceItems = validMovements
            .Where(x => x.ReferenceId.HasValue)
            .GroupBy(x => new
            {
                x.MovementType,
                x.ReferenceId,
                Key = ToKey(x)
            })
            .Where(group => group.Count() > 1);
        foreach (var duplicate in duplicateSourceItems)
        {
            blockers.Add(Blocker(
                InventoryBookBlockerCodes.DuplicateSourceItem,
                "Một nguồn nghiệp vụ có nhiều dòng kho cho cùng một mặt hàng.",
                duplicate.Key.Key,
                duplicate.First().InventoryMovementId));
        }

        var items = new List<InventoryItemPeriodValuation>();
        foreach (var group in validMovements.GroupBy(ToKey))
        {
            var itemBlockers = new List<InventoryBookBlocker>();
            var before = group
                .Where(x => OccurredAt(x) < periodStart)
                .ToList();
            var current = group
                .Where(x => OccurredAt(x) >= periodStart)
                .OrderBy(OccurredAt)
                .ThenBy(x => x.InventoryMovementId)
                .ToList();

            decimal openingQuantity = 0m;
            decimal openingValue = 0m;
            foreach (var movement in before)
            {
                if (IsInbound(movement.MovementType))
                {
                    openingQuantity += movement.Quantity;
                    if (!movement.TotalValue.HasValue)
                    {
                        itemBlockers.Add(Blocker(
                            InventoryBookBlockerCodes.MissingInboundValue,
                            "Phát sinh tăng trước kỳ chưa có giá trị để tính tồn đầu.",
                            group.Key,
                            movement.InventoryMovementId));
                    }
                    else
                    {
                        openingValue += movement.TotalValue.Value;
                    }
                }
                else if (IsOutbound(movement.MovementType))
                {
                    openingQuantity -= movement.Quantity;
                    if (!movement.TotalValue.HasValue)
                    {
                        itemBlockers.Add(Blocker(
                            InventoryBookBlockerCodes.MissingPriorOutboundValue,
                            "Phát sinh xuất của quý trước chưa được chốt giá.",
                            group.Key,
                            movement.InventoryMovementId));
                    }
                    else
                    {
                        openingValue -= movement.TotalValue.Value;
                    }
                }
            }

            var inbound = current.Where(x => IsInbound(x.MovementType)).ToList();
            var outbound = current
                .Where(x => IsOutbound(x.MovementType))
                .OrderBy(OccurredAt)
                .ThenBy(x => x.InventoryMovementId)
                .ToList();
            var inboundQuantity = inbound.Sum(x => x.Quantity);
            var inboundValue = 0m;
            foreach (var movement in inbound)
            {
                if (!movement.TotalValue.HasValue)
                {
                    itemBlockers.Add(Blocker(
                        InventoryBookBlockerCodes.MissingInboundValue,
                        movement.MovementType == InventoryMovementTypes.AdjustmentIn
                            ? "Điều chỉnh tăng chưa có giá trị. Hãy xác nhận trước khi chốt quý."
                            : "Phát sinh nhập chưa có giá trị.",
                        group.Key,
                        movement.InventoryMovementId));
                }
                else
                {
                    inboundValue += movement.TotalValue.Value;
                }
            }

            var outboundQuantity = outbound.Sum(x => x.Quantity);
            var runningQuantity = openingQuantity;
            if (runningQuantity < 0m)
            {
                itemBlockers.Add(Blocker(
                    InventoryBookBlockerCodes.NegativeInventory,
                    "Tồn đầu quý đang âm và cần được kiểm tra trước khi chốt.",
                    group.Key));
            }

            foreach (var movement in current)
            {
                runningQuantity += IsInbound(movement.MovementType)
                    ? movement.Quantity
                    : IsOutbound(movement.MovementType)
                        ? -movement.Quantity
                        : 0m;
                if (runningQuantity < 0m)
                {
                    itemBlockers.Add(Blocker(
                        InventoryBookBlockerCodes.NegativeInventory,
                        "Tồn kho bị âm trong quý và cần được kiểm tra trước khi chốt.",
                        group.Key,
                        movement.InventoryMovementId));
                }
            }

            var valuationBaseQuantity = openingQuantity + inboundQuantity;
            var valuationBaseValue = openingValue + inboundValue;
            decimal? rawAverage = null;
            decimal? displayedAverage = null;
            if (valuationBaseQuantity > 0m && valuationBaseValue >= 0m)
            {
                rawAverage = valuationBaseValue / valuationBaseQuantity;
                displayedAverage = RoundUnit(rawAverage.Value);
            }
            else if (outboundQuantity > 0m)
            {
                itemBlockers.Add(Blocker(
                    InventoryBookBlockerCodes.MissingValuationBase,
                    "Không đủ số lượng và giá trị tồn đầu/nhập trong quý để tính giá xuất kho.",
                    group.Key));
            }

            var outboundValues = rawAverage.HasValue
                ? AllocateOutboundValues(
                    outbound,
                    rawAverage.Value,
                    displayedAverage!.Value,
                    valuationBaseQuantity,
                    valuationBaseValue)
                : [];
            var outboundValue = outboundValues.Sum(x => x.TotalValue);
            var endingQuantity = valuationBaseQuantity - outboundQuantity;
            var endingValue = endingQuantity == 0m
                ? 0m
                : RoundTotal(valuationBaseValue - outboundValue);

            items.Add(new InventoryItemPeriodValuation
            {
                ProductId = group.Key.ProductId,
                IngredientId = group.Key.IngredientId,
                OpeningQuantity = openingQuantity,
                OpeningValue = RoundTotal(openingValue),
                InboundQuantity = inboundQuantity,
                InboundValue = RoundTotal(inboundValue),
                OutboundQuantity = outboundQuantity,
                OutboundValue = RoundTotal(outboundValue),
                EndingQuantity = endingQuantity,
                EndingValue = endingValue,
                WholePeriodAverageUnitValue = displayedAverage,
                OutboundValuations = outboundValues
            });

            blockers.AddRange(itemBlockers);
        }

        return new InventoryPeriodValuation
        {
            PeriodStart = periodStart,
            PeriodEndExclusive = periodEndExclusive,
            IsProvisional = true,
            Items = items,
            Blockers = blockers
        };
    }

    private static List<InventoryOutboundValuation> AllocateOutboundValues(
        IReadOnlyList<InventoryMovement> sortedOutbound,
        decimal rawAverage,
        decimal displayedAverage,
        decimal valuationBaseQuantity,
        decimal valuationBaseValue)
    {
        if (sortedOutbound.Count == 0)
        {
            return [];
        }

        var values = sortedOutbound
            .Select(x => new InventoryOutboundValuation
            {
                InventoryMovementId = x.InventoryMovementId,
                UnitValue = displayedAverage,
                TotalValue = RoundTotal(rawAverage * x.Quantity)
            })
            .ToList();
        var outboundQuantity = sortedOutbound.Sum(x => x.Quantity);
        var targetTotal = outboundQuantity == valuationBaseQuantity
            ? RoundTotal(valuationBaseValue)
            : RoundTotal(rawAverage * outboundQuantity);
        var residual = RoundTotal(targetTotal - values.Sum(x => x.TotalValue));
        var cents = decimal.ToInt32(decimal.Abs(residual) * 100m);
        var direction = decimal.Sign(residual);

        var index = 0;
        while (cents > 0)
        {
            var candidate = values[index % values.Count];
            if (direction > 0 || candidate.TotalValue >= OneCent)
            {
                candidate.TotalValue = RoundTotal(
                    candidate.TotalValue + direction * OneCent);
                cents--;
            }

            index++;
            if (index > values.Count * 10_000)
            {
                throw new InvalidOperationException(
                    "Unable to allocate inventory valuation rounding residual.");
            }
        }

        return values;
    }

    private static void EnsureExactBangkokQuarter(
        DateTime startNaiveUtc,
        DateTime endExclusiveNaiveUtc)
    {
        BangkokBusinessTime.RequireNaiveUtc(
            startNaiveUtc,
            nameof(startNaiveUtc));
        BangkokBusinessTime.RequireNaiveUtc(
            endExclusiveNaiveUtc,
            nameof(endExclusiveNaiveUtc));
        var wallClockStart = BangkokBusinessTime.NaiveUtcToBangkokWallClock(
            startNaiveUtc);
        var isQuarterStart = wallClockStart.Day == 1 &&
                             wallClockStart.Hour == 0 &&
                             wallClockStart.Minute == 0 &&
                             wallClockStart.Second == 0 &&
                             wallClockStart.Millisecond == 0 &&
                             wallClockStart.Month is 1 or 4 or 7 or 10;
        if (!isQuarterStart)
        {
            throw new ArgumentException(
                "Inventory valuation can only finalize an exact Bangkok calendar quarter.");
        }

        var quarter = ((wallClockStart.Month - 1) / 3) + 1;
        var expected = BangkokBusinessTime.GetQuarterNaiveUtc(
            wallClockStart.Year,
            quarter);
        if (expected.StartNaiveUtc != startNaiveUtc ||
            expected.EndExclusiveNaiveUtc != endExclusiveNaiveUtc)
        {
            throw new ArgumentException(
                "Inventory valuation can only finalize an exact Bangkok calendar quarter.");
        }
    }

    private static List<InventoryBookBlocker> ValidateShapes(
        IEnumerable<InventoryMovement> movements)
    {
        var blockers = new List<InventoryBookBlocker>();
        foreach (var movement in movements)
        {
            var key = new InventoryItemKey(
                movement.ProductId,
                movement.IngredientId);
            if (!HasValidItem(movement))
            {
                blockers.Add(Blocker(
                    InventoryBookBlockerCodes.InvalidItem,
                    "Phát sinh kho phải gắn với đúng một hàng hóa hoặc nguyên liệu.",
                    key,
                    movement.InventoryMovementId));
                continue;
            }

            if (!InventoryMovementTypes.All.Contains(movement.MovementType))
            {
                blockers.Add(Blocker(
                    InventoryBookBlockerCodes.InvalidMovementType,
                    "Loại phát sinh kho không hợp lệ.",
                    key,
                    movement.InventoryMovementId));
            }

            if (movement.Quantity <= 0m)
            {
                blockers.Add(Blocker(
                    InventoryBookBlockerCodes.InvalidQuantity,
                    "Số lượng phát sinh kho phải lớn hơn 0.",
                    key,
                    movement.InventoryMovementId));
            }

            if (movement.TotalValue < 0m)
            {
                blockers.Add(Blocker(
                    InventoryBookBlockerCodes.InvalidValue,
                    "Giá trị phát sinh kho không được âm.",
                    key,
                    movement.InventoryMovementId));
            }

            var needsReference = movement.MovementType is
                InventoryMovementTypes.PurchaseIn or InventoryMovementTypes.OrderOut;
            if (needsReference != movement.ReferenceId.HasValue)
            {
                blockers.Add(Blocker(
                    InventoryBookBlockerCodes.InvalidReference,
                    "Tham chiếu nguồn không phù hợp với loại phát sinh kho.",
                    key,
                    movement.InventoryMovementId));
            }
        }

        return blockers;
    }

    private static InventoryBookBlocker Blocker(
        string code,
        string message,
        InventoryItemKey key,
        Guid? movementId = null) => new()
    {
        Code = code,
        Message = message,
        ProductId = key.ProductId,
        IngredientId = key.IngredientId,
        InventoryMovementId = movementId
    };

    private static DateTime OccurredAt(InventoryMovement movement) =>
        BangkokBusinessTime.NormalizeNaiveUtc(movement.OccurredAt);

    private static bool HasValidItem(InventoryMovement movement) =>
        movement.ProductId.HasValue != movement.IngredientId.HasValue &&
        movement.ProductId != Guid.Empty &&
        movement.IngredientId != Guid.Empty;

    private static InventoryItemKey ToKey(InventoryMovement movement) =>
        new(movement.ProductId, movement.IngredientId);

    private static bool IsInbound(string movementType) =>
        movementType is
            InventoryMovementTypes.OpeningBalance or
            InventoryMovementTypes.PurchaseIn or
            InventoryMovementTypes.AdjustmentIn;

    private static bool IsOutbound(string movementType) =>
        movementType is
            InventoryMovementTypes.OrderOut or
            InventoryMovementTypes.AdjustmentOut;

    private static decimal RoundUnit(decimal value) =>
        decimal.Round(value, UnitValueScale, MidpointRounding.AwayFromZero);

    private static decimal RoundTotal(decimal value) =>
        decimal.Round(value, TotalValueScale, MidpointRounding.AwayFromZero);
}
