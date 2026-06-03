namespace TaxMate.Model.DTO;

public class InvoicePdfData
{
    public string InvoiceNumber { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public string BusinessName { get; set; } = null!;
    public string? Address { get; set; }
    public string? TaxCode { get; set; }
    public string? Phone { get; set; }
    public List<InvoiceItemResponse> Items { get; set; } = new();
    public decimal SubTotal { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? SurchargeName { get; set; }
    public decimal SurchargeAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? QRCodeUrl { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
}
