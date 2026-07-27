namespace TaxMate.Model.Documents.Tax;


public sealed class Form01Cnkd2026SummaryModel
{
    // [18] Total
    public decimal TotalRevenue { get; set; }
    public decimal TotalVatNonTaxableRevenue { get; set; }
    public decimal TotalZeroRatedVatRevenue { get; set; }
    public decimal TotalVatTaxAmount { get; set; }
    public decimal TotalPersonalIncomeTaxableRevenue { get; set; }
    public decimal TotalPersonalIncomeTaxDeductibleRevenue { get; set; }
    public decimal TotalPersonalIncomeTaxAmount { get; set; }

    // [19] Tax exemption
    public decimal VatExemptionAmount { get; set; }
    public decimal PersonalIncomeTaxExemptionAmount { get; set; }

    // [20] Remaining tax payable
    public decimal VatPayableAmount { get; set; }
    public decimal PersonalIncomeTaxPayableAmount { get; set; }
}