namespace TaxMate.Model.Documents.Tax;

public class S1aDocumentModel
{
    public string BusinessName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string TaxCode { get; set; } = string.Empty;
    public string BusinessLocation { get; set; } = string.Empty;
    public string DeclarationPeriod { get; set; } = string.Empty;
    public string Unit { get; set; } = "VNĐ";
    
    public List<S1aDocumentLineModel> Lines { get; set; } = new();
}
