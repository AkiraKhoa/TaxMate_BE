using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO.Inventory;

public sealed class InventoryOpeningLineRequest
{
    public Guid? ProductId { get; set; }

    public Guid? IngredientId { get; set; }

    public decimal Quantity { get; set; }

    public decimal? TotalValue { get; set; }
}

public sealed class InventoryCountLineRequest
{
    public Guid? ProductId { get; set; }

    public Guid? IngredientId { get; set; }

    public decimal ActualQuantity { get; set; }

    /// <summary>
    /// Required only when the actual count creates an AdjustmentIn.
    /// It is the value of the positive difference, not the whole ending stock.
    /// </summary>
    public decimal? AdjustmentInTotalValue { get; set; }
}

public sealed class InitializeInventoryRequest
{
    public DateTime OccurredAt { get; set; }

    [Required]
    [MaxLength(100)]
    public string DocumentNumber { get; set; } = null!;

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = null!;

    public IReadOnlyList<InventoryOpeningLineRequest> Lines { get; set; } = [];
}

public sealed class ReconcileInventoryRequest
{
    public string? ExpectedVersion { get; set; }

    // Kept for older clients; the server always replaces this with its own time.
    public DateTime OccurredAt { get; set; }

    [Required]
    [MaxLength(100)]
    public string DocumentNumber { get; set; } = null!;

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = null!;

    public IReadOnlyList<InventoryCountLineRequest> Lines { get; set; } = [];
}

public sealed class InventoryControlItemResponse
{
    public Guid? ProductId { get; set; }

    public Guid? IngredientId { get; set; }

    public string Name { get; set; } = null!;

    public string? Unit { get; set; }

    public decimal CurrentQuantity { get; set; }

    public decimal? CurrentUnitValue { get; set; }
}

public sealed class InventoryInitializationPreviewResponse
{
    public string Version { get; set; } = string.Empty;
    public Guid BusinessId { get; set; }

    public bool IsInitialized { get; set; }

    public bool IsStockTrackingEnabled { get; set; }

    public IReadOnlyList<InventoryControlItemResponse> Items { get; set; } = [];
}

public sealed class InventoryControlResultResponse
{
    public Guid BusinessId { get; set; }

    public bool IsStockTrackingEnabled { get; set; }

    public int OpeningBalanceCount { get; set; }

    public int AdjustmentInCount { get; set; }

    public int AdjustmentOutCount { get; set; }

    public IReadOnlyList<InventoryControlItemResponse> Items { get; set; } = [];
}
