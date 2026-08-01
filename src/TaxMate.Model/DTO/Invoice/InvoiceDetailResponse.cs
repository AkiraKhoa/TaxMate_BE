namespace TaxMate.Model.DTO;

public class InvoiceDetailResponse
{
    public string InvoiceNumber { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public string Status { get; set; } = null!;
    public string BusinessName { get; set; } = null!;
    public string? Address { get; set; }
    public List<InvoiceItemResponse> Items { get; set; } = new();
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal SurchargeAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? PdfUrl { get; set; }
}
