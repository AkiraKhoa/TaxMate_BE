namespace TaxMate.Model.DTO;

public class OrderSummaryResponse
{
    public Guid TransactionId { get; set; }
    public string TransactionCode { get; set; } = null!;
    public DateTime TransactionDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = null!;
    public int ItemCount { get; set; }
    public string? InvoiceNumber { get; set; }
}
