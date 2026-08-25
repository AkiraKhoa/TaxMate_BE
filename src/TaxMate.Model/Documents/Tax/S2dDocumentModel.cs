using TaxMate.Model.DTO.Inventory;

namespace TaxMate.Model.Documents.Tax;

public sealed class S2dDocumentModel
{
    public string BusinessName { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string TaxCode { get; set; } = string.Empty;

    public string RepresentativeName { get; set; } = string.Empty;

    public int Year { get; set; }

    public int Quarter { get; set; }

    public DateTime ExportDate { get; set; }

    public S2dBook Book { get; set; } = null!;
}
