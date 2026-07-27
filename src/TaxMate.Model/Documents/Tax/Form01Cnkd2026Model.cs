namespace TaxMate.Model.Documents.Tax;

public sealed class Form01Cnkd2026Model
{
    // Header / declaration method
    public string TaxMethod { get; set; } = "RevenueBased";

    public string PeriodType { get; set; } = null!;
    public int Year { get; set; }
    public int? Month { get; set; }
    public int? Quarter { get; set; }

    public bool IsInitialDeclaration { get; set; }
    public int? SupplementNumber { get; set; }

    public string TaxpayerName { get; set; } = null!;
    public string TaxCode { get; set; } = null!;

    public string? AuthorizedDeclarerName { get; set; }
    public string? AuthorizedDeclarerTaxCode { get; set; }
    public string? AuthorizationNumber { get; set; }
    public DateTime? AuthorizationDate { get; set; }

    public string? TaxAgentName { get; set; }
    public string? TaxAgentTaxCode { get; set; }
    
    public DateTime DeclarationDate { get; set; }

    public string? SignerName { get; set; }
    
    public decimal TotalPitTaxableRevenue { get; set; }

    public decimal TotalPitDeductibleRevenue { get; set; }

    public decimal TotalPitRevenue { get; set; }

    public decimal TotalPitTaxAmount { get; set; }

    public decimal RemainingPitDeduction { get; set; }

    public List<Form01Cnkd2026LineModel> Lines { get; set; } = [];
    public Form01Cnkd2026SummaryModel Summary { get; set; } = new();
    public List<Form01Cnkd2026PaymentLineModel> PaymentLines { get; set; } = [];
}