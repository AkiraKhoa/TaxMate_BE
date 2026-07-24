namespace TaxMate.Model.DTO.TaxDeclaration;

public class TaxDeclarationLineResponse
{
    public Guid Id { get; set; }

    public string SectionCode { get; set; } = null!;

    public string IndicatorCode { get; set; } = null!;

    public string BusinessActivityCode { get; set; } = null!;

    public string BusinessActivityName { get; set; } = null!;

    public decimal TotalRevenue { get; set; }

    public decimal VatTaxableRevenue { get; set; }

    public decimal ZeroRatedVatRevenue { get; set; }

    public decimal VatTaxRate { get; set; }

    public decimal VatTaxAmount { get; set; }

    public decimal PersonalIncomeTaxableRevenue { get; set; }

    public decimal PersonalIncomeTaxDeductibleRevenue { get; set; }

    public decimal PersonalIncomeTaxRate { get; set; }

    public decimal PersonalIncomeTaxAmount { get; set; }
    
    public decimal VatNonTaxableRevenue { get; set; }

    public decimal PersonalIncomeTaxRevenue { get; set; }
}