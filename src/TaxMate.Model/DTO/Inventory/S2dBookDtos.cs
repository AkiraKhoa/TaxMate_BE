namespace TaxMate.Model.DTO.Inventory;

public sealed class S2dBook
{
    public Guid BusinessId { get; set; }

    public DateTime PeriodStart { get; set; }

    public DateTime PeriodEndExclusive { get; set; }

    public bool IsProvisional { get; set; }

    public bool CanFinalize => Blockers.Count == 0;

    public IReadOnlyList<S2dItemBook> Items { get; set; } = [];

    public IReadOnlyList<InventoryBookBlocker> Blockers { get; set; } = [];
}

public sealed class S2dItemBook
{
    public Guid? ProductId { get; set; }

    public Guid? IngredientId { get; set; }

    public string ItemCode { get; set; } = string.Empty;

    public string ItemName { get; set; } = null!;

    public string? Unit { get; set; }

    public bool IsDeleted { get; set; }

    public decimal OpeningQuantity { get; set; }

    public decimal OpeningValue { get; set; }

    public decimal TotalInboundQuantity { get; set; }

    public decimal TotalInboundValue { get; set; }

    public decimal TotalOutboundQuantity { get; set; }

    public decimal TotalOutboundValue { get; set; }

    public decimal EndingQuantity { get; set; }

    public decimal EndingValue { get; set; }

    public decimal? WholePeriodAverageUnitValue { get; set; }

    public IReadOnlyList<S2dBookLine> Lines { get; set; } = [];
}

public sealed class S2dBookLine
{
    public Guid InventoryMovementId { get; set; }

    public DateTime DocumentDate { get; set; }

    public string DocumentNumber { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string MovementType { get; set; } = null!;

    public Guid? ReferenceId { get; set; }

    public decimal? InboundUnitValue { get; set; }

    public decimal? InboundQuantity { get; set; }

    public decimal? InboundValue { get; set; }

    public decimal? OutboundUnitValue { get; set; }

    public decimal? OutboundQuantity { get; set; }

    public decimal? OutboundValue { get; set; }

    public decimal RunningQuantity { get; set; }

    public decimal RunningValue { get; set; }

    public bool IsProvisionalValue { get; set; }
}
