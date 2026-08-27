namespace TaxMate.Model.DTO.Inventory;

public sealed class InventoryBookBlocker
{
    public string Code { get; set; } = null!;

    public string Message { get; set; } = null!;

    public Guid? ProductId { get; set; }

    public Guid? IngredientId { get; set; }

    public Guid? InventoryMovementId { get; set; }
}

public sealed class InventoryOutboundValuation
{
    public Guid InventoryMovementId { get; set; }

    public decimal UnitValue { get; set; }

    public decimal TotalValue { get; set; }
}

public sealed class InventoryItemPeriodValuation
{
    public Guid? ProductId { get; set; }

    public Guid? IngredientId { get; set; }

    public decimal OpeningQuantity { get; set; }

    public decimal OpeningValue { get; set; }

    public decimal InboundQuantity { get; set; }

    public decimal InboundValue { get; set; }

    public decimal OutboundQuantity { get; set; }

    public decimal OutboundValue { get; set; }

    public decimal EndingQuantity { get; set; }

    public decimal EndingValue { get; set; }

    public decimal? WholePeriodAverageUnitValue { get; set; }

    public IReadOnlyList<InventoryOutboundValuation> OutboundValuations { get; set; }
        = [];
}

public sealed class InventoryPeriodValuation
{
    public DateTime PeriodStart { get; set; }

    public DateTime PeriodEndExclusive { get; set; }

    public bool IsProvisional { get; set; }

    public bool CanFinalize => Blockers.Count == 0;

    public IReadOnlyList<InventoryItemPeriodValuation> Items { get; set; } = [];

    public IReadOnlyList<InventoryBookBlocker> Blockers { get; set; } = [];
}
