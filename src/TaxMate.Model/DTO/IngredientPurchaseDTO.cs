using System;
using System.Collections.Generic;

namespace TaxMate.Model.DTO;

public class CreateIngredientPurchaseRequest
{
    public Guid IngredientId { get; set; }
    public decimal Quantity { get; set; }
    public decimal TotalCost { get; set; }
    public DateTime PurchaseDate { get; set; }
    public string? InvoiceNumber { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? ReceiptImageUrl { get; set; }
}

public class UpdateIngredientPurchaseRequest
{
    public Guid IngredientId { get; set; }
    public decimal Quantity { get; set; }
    public decimal TotalCost { get; set; }
    public DateTime PurchaseDate { get; set; }
    public string? InvoiceNumber { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? ReceiptImageUrl { get; set; }
}

public class CreateBatchIngredientPurchaseRequest
{
    public string? InvoiceNumber { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? ReceiptImageUrl { get; set; }
    public DateTime PurchaseDate { get; set; }
    public List<BatchPurchaseItem> Items { get; set; } = new();
}

public class BatchPurchaseItem
{
    public Guid IngredientId { get; set; }
    public decimal Quantity { get; set; }
    public decimal TotalCost { get; set; }
}

public class IngredientPurchaseResponse
{
    public Guid Id { get; set; }
    public Guid? ExpenseId { get; set; }
    public Guid BusinessId { get; set; }
    public string BusinessName { get; set; } = null!;
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = null!;
    public string? IngredientUnit { get; set; }
    public decimal Quantity { get; set; }
    public decimal TotalCost { get; set; }
    public DateTime PurchaseDate { get; set; }
    public string? InvoiceNumber { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? ReceiptImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
