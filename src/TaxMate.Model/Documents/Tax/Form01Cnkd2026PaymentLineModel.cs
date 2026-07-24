namespace TaxMate.Model.Documents.Tax;

public class Form01Cnkd2026PaymentLineModel
{
    public string? BusinessLocationCode { get; set; }

    public string StateBudgetContent { get; set; } = null!;

    public decimal Amount { get; set; }

    public string? ChapterCode { get; set; }

    public string? SubsectionCode { get; set; }

    public string? AdministrativeAreaCode { get; set; }

    public string? CollectingAuthority { get; set; }

    public string? TaxAuthority { get; set; }

    public DateTime? DueDate { get; set; }
}