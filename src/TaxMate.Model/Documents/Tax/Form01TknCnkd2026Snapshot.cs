namespace TaxMate.Model.Documents.Tax;

public sealed record Form01TknCnkd2026Snapshot
{
    public int SchemaVersion { get; init; } = 1;
    public string FormCode { get; init; } = "01/TKN-CNKD";
    public string LegalTemplateVersion { get; init; } = "TT89/2026";
    public DateOnly LegalEffectiveDate { get; init; } = new(2026, 7, 1);
    public Guid DeclarationId { get; init; }
    public string DeclarationCode { get; init; } = null!;
    public int DeclarationVersion { get; init; }
    public string DeclarationType { get; init; } = null!;
    public int? SupplementNumber { get; init; }
    public DateTime GeneratedAt { get; init; }
    public string PeriodSelector { get; init; } = null!;
    public int Year { get; init; }
    public DateTime WindowStart { get; init; }
    public DateTime WindowEnd { get; init; }
    public DateTime? DueDate { get; init; }
    public bool IsAtOrBelowOneBillion { get; init; } = true;
    public bool IsNewBusinessAtOrBelowOneBillion { get; init; }
    public string TaxpayerName { get; init; } = null!;
    public string TaxCode { get; init; } = null!;
    public string? TaxpayerAddress { get; init; }
    public string? AuthorizedDeclarerName { get; init; }
    public string? AuthorizedDeclarerTaxCode { get; init; }
    public string? TaxAgentName { get; init; }
    public string? TaxAgentTaxCode { get; init; }
    public string? TaxAgentContractNumber { get; init; }
    public DateTime? TaxAgentContractDate { get; init; }
    public decimal AnnualRevenueAtGeneration { get; init; }
    public decimal ApplicableThreshold { get; init; }
    public string? CalculationRuleVersion { get; init; }
    public List<Form01TknCnkd2026LineSnapshot> SectionALines { get; init; } = [];
}

public sealed record Form01TknCnkd2026LineSnapshot(
    string SectionCode, string IndicatorCode, string BusinessActivityCode,
    string BusinessActivityName, Guid? BusinessLocationId,
    string? BusinessLocationCode, decimal TotalRevenue,
    decimal VatNonTaxableRevenue, decimal ZeroRatedVatRevenue,
    decimal VatTaxAmount, decimal PersonalIncomeTaxableRevenue,
    decimal PersonalIncomeTaxDeductibleRevenue,
    decimal PersonalIncomeTaxAmount, int DisplayOrder);
