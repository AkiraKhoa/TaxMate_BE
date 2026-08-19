namespace TaxMate.Model.Documents.Tax;

public class S1aDocumentModel
{
    public string TaxCode { get; set; } = string.Empty;
    public string DeclarationPeriod { get; set; } = string.Empty;
    public string Unit { get; set; } = "VNĐ";

    public List<S1aBusinessSectionModel> Businesses { get; set; } = new();
}
