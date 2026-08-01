namespace TaxMate.Model.DTO;

public class InvoiceItemResponse
{
    public string ProductName { get; set; } = null!;
    public string? Unit { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal LineTotal { get; set; }
}
