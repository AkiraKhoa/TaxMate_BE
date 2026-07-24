namespace TaxMate.Model.DTO.TaxDeclaration;

public class TaxDeclarationResponse
{
    public Guid Id { get; set; }

    public Guid TaxPeriodId { get; set; }

    public Guid TaxCalculationId { get; set; }

    public string FormCode { get; set; } = null!;

    public string DeclarationCode { get; set; } = null!;

    public int Version { get; set; }

    public string DeclarationType { get; set; } = null!;

    public int? SupplementNumber { get; set; }

    public string Status { get; set; } = null!;

    public string TaxpayerName { get; set; } = null!;

    public string TaxCode { get; set; } = null!;

    public string? TaxpayerAddress { get; set; }

    public decimal TotalRevenue { get; set; }

    public decimal TotalVatTaxAmount { get; set; }

    public decimal TotalPersonalIncomeTaxAmount { get; set; }

    public decimal VatExemptionAmount { get; set; }

    public decimal PersonalIncomeTaxExemptionAmount { get; set; }

    public decimal VatPayableAmount { get; set; }

    public decimal PersonalIncomeTaxPayableAmount { get; set; }

    public decimal TotalTaxPayableAmount { get; set; }

    public DateTime GeneratedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public List<TaxDeclarationLineResponse> Lines { get; set; } = [];
}