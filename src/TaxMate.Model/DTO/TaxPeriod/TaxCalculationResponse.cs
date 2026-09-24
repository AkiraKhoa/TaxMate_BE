namespace TaxMate.Model.DTO.TaxPeriod;

public class TaxCalculationResponse
{
    public Guid TaxPeriodId { get; set; }

    public Guid TaxCalculationId { get; set; }

    public int Version { get; set; }

    public string TaxMethod { get; set; } = null!;

    public int? TaxMethodEffectiveYear { get; set; }

    public string? CalculationRuleVersion { get; set; }

    public decimal TotalRevenue { get; set; }

    public decimal TotalTaxableRevenue { get; set; }

    public decimal TotalVatTaxAmount { get; set; }

    public decimal TotalPersonalIncomeTaxAmount { get; set; }

    public decimal TotalTaxBeforeExemption { get; set; }

    public decimal TotalExemptionAmount { get; set; }

    public decimal TotalTaxPayableAmount { get; set; }
    
    public decimal AnnualRevenueAtCalculation { get; set; }

    public decimal ApplicableRevenueThreshold { get; set; }

    public string RecommendedFormCode { get; set; } = null!;

    public decimal RemainingPitDeduction { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CalculatedAt { get; set; }

    public List<TaxCalculationLineResponse> Lines { get; set; } = [];
}

public class TaxCalculationLineResponse
{
    public Guid Id { get; set; }

    public Guid? BusinessCategoryId { get; set; }

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
