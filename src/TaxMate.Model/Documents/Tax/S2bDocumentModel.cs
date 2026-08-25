namespace TaxMate.Model.Documents.Tax;

public sealed class S2bDocumentModel
{
    public string BusinessName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string TaxCode { get; set; } = string.Empty;
    public string BusinessLocation { get; set; } = string.Empty;
    public string RepresentativeName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Quarter { get; set; }
    public DateTime ExportDate { get; set; }
    public IReadOnlyList<S2bDocumentGroupModel> Groups { get; set; } = [];
}

public sealed class S2bDocumentGroupModel
{
    public string BusinessCategoryName { get; set; } = string.Empty;
    public decimal VatRate { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal VatAmount { get; set; }
    public IReadOnlyList<S2bDocumentLineModel> Lines { get; set; } = [];
}

public sealed class S2bDocumentLineModel
{
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
