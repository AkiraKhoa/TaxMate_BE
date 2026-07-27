namespace TaxMate.Model.Documents.Tax;

public class Form01Cnkd2026LineModel
{
    public string SectionCode { get; set; } = null!;

    public string ActivityCode { get; set; } = null!;

    public string ActivityName { get; set; } = null!;

    public string? BusinessLocationCode { get; set; }

    public string? BusinessLocationName { get; set; }

    // [10]
    public decimal TotalRevenue { get; set; }

    // [11]
    public decimal VatNonTaxableRevenue { get; set; }

    // [12]
    public decimal ZeroRatedVatRevenue { get; set; }

    // [13]
    public decimal VatTaxAmount { get; set; }

    // [14]
    public decimal PersonalIncomeTaxableRevenue { get; set; }

    // [15]
    public decimal PersonalIncomeTaxDeductibleRevenue { get; set; }

    // [16]
    public decimal PersonalIncomeTaxRevenue { get; set; }

    // [17]
    public decimal PersonalIncomeTaxAmount { get; set; }

    public int DisplayOrder { get; set; }
}