namespace TaxMate.Model.Documents.Tax;

public class S1aDocumentLineModel
{
    public string Date { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal RevenueAmount { get; set; }
}
