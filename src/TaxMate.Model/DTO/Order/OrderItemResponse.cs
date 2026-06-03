namespace TaxMate.Model.DTO;

public class OrderItemResponse
{
    public Guid TransactionItemId { get; set; }
    public Guid? ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string? Unit { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Quantity { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public string? Note { get; set; }
}
