using TaxMate.Model.Common;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.Entities;
using TaxMate.Service.Common;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

internal sealed class S2dBookProjector : IS2dBookProjector
{
    private readonly IInventoryValuationService _valuation;

    public S2dBookProjector(IInventoryValuationService valuation)
    {
        _valuation = valuation;
    }

    public S2dBook ProjectQuarter(
        Guid businessId,
        IReadOnlyCollection<InventoryMovement> movementsBeforePeriodEnd,
        int year,
        int quarter,
        bool requireFinalValues = false)
    {
        ArgumentNullException.ThrowIfNull(movementsBeforePeriodEnd);
        var (periodStart, periodEndExclusive) =
            BangkokBusinessTime.GetQuarterNaiveUtc(year, quarter);
        var scoped = movementsBeforePeriodEnd
            .Where(x => x.BusinessId == businessId)
            .Where(x => OccurredAt(x) < periodEndExclusive)
            .ToList();
        var valuation = _valuation.PreviewQuarter(
            scoped,
            year,
            quarter);
        var blockers = valuation.Blockers.ToList();

        foreach (var foreign in movementsBeforePeriodEnd.Where(x =>
                     x.BusinessId != businessId &&
                     OccurredAt(x) < periodEndExclusive))
        {
            blockers.Add(new InventoryBookBlocker
            {
                Code = InventoryBookBlockerCodes.ItemBusinessMismatch,
                Message = "Phát sinh kho không thuộc cửa hàng đang lập sổ.",
                ProductId = foreign.ProductId,
                IngredientId = foreign.IngredientId,
                InventoryMovementId = foreign.InventoryMovementId
            });
        }

        var valuationByItem = valuation.Items.ToDictionary(x =>
            new InventoryItemKey(x.ProductId, x.IngredientId));
        var provisionalOutboundValues = valuation.Items
            .SelectMany(x => x.OutboundValuations)
            .ToDictionary(x => x.InventoryMovementId, x => x.TotalValue);
        var itemBooks = new List<S2dItemBook>();

        foreach (var group in scoped
                     .Where(HasValidItem)
                     .GroupBy(ToKey))
        {
            if (!valuationByItem.TryGetValue(group.Key, out var itemValuation))
            {
                continue;
            }

            var ordered = group
                .OrderBy(OccurredAt)
                .ThenBy(x => x.CreatedAt)
                .ThenBy(x => x.InventoryMovementId)
                .ToList();
            var sample = ordered[0];
            var metadata = ResolveMetadata(sample);
            if (!metadata.Exists)
            {
                blockers.Add(new InventoryBookBlocker
                {
                    Code = InventoryBookBlockerCodes.ItemNotFound,
                    Message = "Không còn tìm thấy thông tin mặt hàng của dòng S2d.",
                    ProductId = group.Key.ProductId,
                    IngredientId = group.Key.IngredientId,
                    InventoryMovementId = sample.InventoryMovementId
                });
            }

            if (string.IsNullOrWhiteSpace(metadata.Unit))
            {
                blockers.Add(new InventoryBookBlocker
                {
                    Code = InventoryBookBlockerCodes.MissingUnit,
                    Message = "Mặt hàng có phát sinh nhưng chưa có đơn vị tính.",
                    ProductId = group.Key.ProductId,
                    IngredientId = group.Key.IngredientId
                });
            }

            var current = ordered
                .Where(x =>
                    OccurredAt(x) >= periodStart &&
                    OccurredAt(x) < periodEndExclusive)
                .ToList();
            decimal runningQuantity = itemValuation.OpeningQuantity;
            decimal runningValue = itemValuation.OpeningValue;
            var lines = new List<S2dBookLine>(current.Count);

            if (runningQuantity < 0m)
            {
                AddNegativeBlocker(blockers, group.Key, null);
            }

            foreach (var movement in current)
            {
                var inbound = IsInbound(movement.MovementType);
                decimal? lineValue = movement.TotalValue;
                var provisional = false;

                if (!inbound && IsOutbound(movement.MovementType))
                {
                    if (requireFinalValues)
                    {
                        if (!lineValue.HasValue)
                        {
                            blockers.Add(new InventoryBookBlocker
                            {
                                Code = InventoryBookBlockerCodes.MissingOutboundValue,
                                Message = "Phát sinh xuất chưa có giá trị chính thức của kỳ đã chốt.",
                                ProductId = group.Key.ProductId,
                                IngredientId = group.Key.IngredientId,
                                InventoryMovementId = movement.InventoryMovementId
                            });
                        }
                    }

                    if (!lineValue.HasValue && provisionalOutboundValues.TryGetValue(
                            movement.InventoryMovementId,
                            out var calculated))
                    {
                        lineValue = calculated;
                        provisional = true;
                    }
                }

                if (inbound)
                {
                    runningQuantity += movement.Quantity;
                    runningValue += lineValue ?? 0m;
                }
                else if (IsOutbound(movement.MovementType))
                {
                    runningQuantity -= movement.Quantity;
                    runningValue -= lineValue ?? 0m;
                }

                runningValue = RoundTotal(runningValue);
                if (runningQuantity < 0m)
                {
                    AddNegativeBlocker(
                        blockers,
                        group.Key,
                        movement.InventoryMovementId);
                }

                var unitValue = !inbound && IsOutbound(movement.MovementType)
                    ? itemValuation.WholePeriodAverageUnitValue
                    : lineValue.HasValue
                        ? RoundUnit(lineValue.Value / movement.Quantity)
                        : (decimal?)null;
                lines.Add(new S2dBookLine
                {
                    InventoryMovementId = movement.InventoryMovementId,
                    DocumentDate = OccurredAt(movement),
                    DocumentNumber = movement.DocumentNumber,
                    Description = movement.Description,
                    MovementType = movement.MovementType,
                    ReferenceId = movement.ReferenceId,
                    InboundUnitValue = inbound ? unitValue : null,
                    InboundQuantity = inbound ? movement.Quantity : null,
                    InboundValue = inbound ? lineValue : null,
                    OutboundUnitValue = inbound ? null : unitValue,
                    OutboundQuantity = inbound ? null : movement.Quantity,
                    OutboundValue = inbound ? null : lineValue,
                    RunningQuantity = runningQuantity,
                    RunningValue = runningValue,
                    IsProvisionalValue = provisional || !requireFinalValues && !inbound
                });
            }

            var totalInboundValue = current
                .Where(x => IsInbound(x.MovementType))
                .Sum(x => x.TotalValue ?? 0m);
            var totalOutboundValue = lines.Sum(x => x.OutboundValue ?? 0m);
            var include = itemValuation.OpeningQuantity != 0m ||
                          itemValuation.OpeningValue != 0m ||
                          current.Any() ||
                          runningQuantity != 0m ||
                          runningValue != 0m;
            if (!include)
            {
                continue;
            }

            itemBooks.Add(new S2dItemBook
            {
                ProductId = group.Key.ProductId,
                IngredientId = group.Key.IngredientId,
                ItemCode = metadata.Code,
                ItemName = metadata.Name,
                Unit = metadata.Unit,
                IsDeleted = metadata.IsDeleted,
                OpeningQuantity = itemValuation.OpeningQuantity,
                OpeningValue = itemValuation.OpeningValue,
                TotalInboundQuantity = current
                    .Where(x => IsInbound(x.MovementType))
                    .Sum(x => x.Quantity),
                TotalInboundValue = RoundTotal(totalInboundValue),
                TotalOutboundQuantity = current
                    .Where(x => IsOutbound(x.MovementType))
                    .Sum(x => x.Quantity),
                TotalOutboundValue = RoundTotal(totalOutboundValue),
                EndingQuantity = runningQuantity,
                EndingValue = runningValue,
                WholePeriodAverageUnitValue = itemValuation.WholePeriodAverageUnitValue,
                Lines = lines
            });
        }

        var missingFinal = requireFinalValues && blockers.Any(x =>
            x.Code == InventoryBookBlockerCodes.MissingOutboundValue);
        return new S2dBook
        {
            BusinessId = businessId,
            PeriodStart = periodStart,
            PeriodEndExclusive = periodEndExclusive,
            IsProvisional = !requireFinalValues || missingFinal,
            Items = itemBooks
                .OrderBy(x => x.ItemName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.ProductId)
                .ThenBy(x => x.IngredientId)
                .ToList(),
            Blockers = Deduplicate(blockers)
        };
    }

    private static IReadOnlyList<InventoryBookBlocker> Deduplicate(
        IEnumerable<InventoryBookBlocker> blockers) =>
        blockers
            .GroupBy(x => new
            {
                x.Code,
                x.ProductId,
                x.IngredientId,
                x.InventoryMovementId
            })
            .Select(group => group.First())
            .ToList();

    private static void AddNegativeBlocker(
        ICollection<InventoryBookBlocker> blockers,
        InventoryItemKey key,
        Guid? movementId)
    {
        blockers.Add(new InventoryBookBlocker
        {
            Code = InventoryBookBlockerCodes.NegativeInventory,
            Message = "Tồn kho bị âm trong kỳ và cần được kiểm tra trước khi chốt.",
            ProductId = key.ProductId,
            IngredientId = key.IngredientId,
            InventoryMovementId = movementId
        });
    }

    private static ItemMetadata ResolveMetadata(InventoryMovement movement)
    {
        if (movement.ProductId.HasValue)
        {
            return movement.Product is null
                ? ItemMetadata.Missing
                : new ItemMetadata(
                    true,
                    movement.Product.ProductCode,
                    movement.Product.Name,
                    movement.Product.Unit,
                    movement.Product.IsDeleted);
        }

        return movement.Ingredient is null
            ? ItemMetadata.Missing
            : new ItemMetadata(
                true,
                string.Empty,
                movement.Ingredient.Name,
                movement.Ingredient.Unit,
                movement.Ingredient.IsDeleted);
    }

    private static bool HasValidItem(InventoryMovement movement) =>
        movement.ProductId.HasValue != movement.IngredientId.HasValue &&
        movement.ProductId != Guid.Empty &&
        movement.IngredientId != Guid.Empty;

    private static InventoryItemKey ToKey(InventoryMovement movement) =>
        new(movement.ProductId, movement.IngredientId);

    private static DateTime OccurredAt(InventoryMovement movement) =>
        BangkokBusinessTime.NormalizeNaiveUtc(movement.OccurredAt);

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
        decimal.Round(value, 6, MidpointRounding.AwayFromZero);

    private static decimal RoundTotal(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record ItemMetadata(
        bool Exists,
        string Code,
        string Name,
        string? Unit,
        bool IsDeleted)
    {
        public static ItemMetadata Missing { get; } =
            new(false, string.Empty, "Không xác định", null, false);
    }
}
