using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class IngredientPurchase : BaseEntity
{
    public Guid Id { get; set; }

    public Guid IngredientId { get; set; }

    public Guid BusinessId { get; set; }

    public Guid? ExpenseId { get; set; }

    [Precision(18,3)]
    public decimal Quantity { get; set; }

    [Precision(18,2)]
    public decimal TotalCost { get; set; }

    public DateTime PurchaseDate { get; set; }

    [MaxLength(100)]
    public string? InvoiceNumber { get; set; }

    [MaxLength(200)]
    public string? SupplierName { get; set; }

    [MaxLength(1000)]
    public string? ReceiptImageUrl { get; set; }

    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public Ingredient Ingredient { get; set; } = null!;

    public BusinessProfile Business { get; set; } = null!;

    public Expense? Expense { get; set; }
}
