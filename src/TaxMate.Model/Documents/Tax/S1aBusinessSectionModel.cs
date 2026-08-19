namespace TaxMate.Model.Documents.Tax;

public class S1aBusinessSectionModel
{
    public string BusinessName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string BusinessLocation { get; set; } = string.Empty;
    public List<S1aDocumentLineModel> Lines { get; set; } = new();
    public decimal VatRate { get; set; }
    public decimal PitRate { get; set; }
    public decimal VatTax { get; set; }
    public decimal PitTax { get; set; }
}
