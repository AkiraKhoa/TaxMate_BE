using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO.InventoryPurchase;

public sealed class InventoryPurchaseLineRequest
{
    public Guid? ProductId { get; set; }

    public Guid? IngredientId { get; set; }

    public decimal Quantity { get; set; }

    public decimal TotalValue { get; set; }
}

public abstract class InventoryPurchaseWriteRequest
{
    public Guid ExpenseCategoryId { get; set; }

    [MaxLength(100)]
    public string? VoucherNumber { get; set; }

    [Required]
    [MaxLength(200)]
    public string ExpenseTitle { get; set; } = null!;

    public DateTime PurchaseDate { get; set; }

    public Guid? SupplierId { get; set; }

    [MaxLength(1000)]
    public string? ReceiptImageUrl { get; set; }

    [MaxLength(1000)]
    public string? FileUrl { get; set; }

    [MaxLength(2000)]
    public string? Note { get; set; }

    public DateTime? DueDate { get; set; }

    public DateTime? PaidDate { get; set; }

    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    public Guid? PaymentAccountId { get; set; }

    [MinLength(1)]
    public List<InventoryPurchaseLineRequest> Lines { get; set; } = [];
}

public sealed class CreateInventoryPurchaseRequest
    : InventoryPurchaseWriteRequest;

public sealed class UpdateInventoryPurchaseRequest
    : InventoryPurchaseWriteRequest;

public sealed class InventoryPurchaseLineResponse
{
    public Guid? ProductId { get; set; }

    public Guid? IngredientId { get; set; }

    public string ItemName { get; set; } = null!;

    public string? Unit { get; set; }

    public decimal Quantity { get; set; }

    public decimal TotalValue { get; set; }
}

public sealed class InventoryPurchaseResponse
{
    public Guid ExpenseId { get; set; }

    public Guid BusinessId { get; set; }

    public Guid ExpenseCategoryId { get; set; }

    public string? ExpenseCategoryName { get; set; }

    public string VoucherNumber { get; set; } = null!;

    public string ExpenseTitle { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateTime PurchaseDate { get; set; }

    public Guid? SupplierId { get; set; }

    public string? SupplierName { get; set; }

    public string? ReceiptImageUrl { get; set; }

    public string? FileUrl { get; set; }

    public string? Note { get; set; }

    public DateTime? DueDate { get; set; }

    public DateTime? PaidDate { get; set; }

    public string? PaymentMethod { get; set; }

    public Guid? PaymentAccountId { get; set; }

    public IReadOnlyList<InventoryPurchaseLineResponse> Lines { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
