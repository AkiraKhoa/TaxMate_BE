namespace TaxMate.Model.DTO.Inventory;

public readonly record struct InventoryItemKey(
    Guid? ProductId,
    Guid? IngredientId)
{
    public static InventoryItemKey ForProduct(Guid productId) =>
        new(productId, null);

    public static InventoryItemKey ForIngredient(Guid ingredientId) =>
        new(null, ingredientId);
}

public sealed record InventoryMovementReferenceTarget(
    Guid BusinessId,
    string MovementType,
    Guid ReferenceId);

public sealed class InventoryMovementLineInput
{
    public Guid? ProductId { get; set; }

    public Guid? IngredientId { get; set; }

    public decimal Quantity { get; set; }

    public decimal? TotalValue { get; set; }
}

public sealed class ReplaceInventorySourceMovementsCommand
{
    public Guid BusinessId { get; set; }

    public string MovementType { get; set; } = null!;

    public Guid ReferenceId { get; set; }

    public DateTime OccurredAt { get; set; }

    public string DocumentNumber { get; set; } = null!;

    public string Description { get; set; } = null!;

    public IReadOnlyList<InventoryMovementLineInput> Lines { get; set; } = [];
}

public sealed class StageInventoryOpeningBalancesCommand
{
    public Guid BusinessId { get; set; }

    public DateTime OccurredAt { get; set; }

    public string DocumentNumber { get; set; } = null!;

    public string Description { get; set; } = null!;

    public IReadOnlyList<InventoryMovementLineInput> Lines { get; set; } = [];
}

public sealed class StageInventoryAdjustmentCommand
{
    public Guid BusinessId { get; set; }

    public string MovementType { get; set; } = null!;

    public Guid? ProductId { get; set; }

    public Guid? IngredientId { get; set; }

    public decimal Quantity { get; set; }

    public decimal? TotalValue { get; set; }

    public DateTime OccurredAt { get; set; }

    public string DocumentNumber { get; set; } = null!;

    public string Description { get; set; } = null!;
}
