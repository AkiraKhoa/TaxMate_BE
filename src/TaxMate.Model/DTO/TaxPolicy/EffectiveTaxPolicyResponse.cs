namespace TaxMate.Model.DTO.TaxPolicy;

public class EffectiveTaxPolicyResponse
{
    public DateOnly EffectiveOn { get; set; }

    public decimal AnnualRevenueThreshold { get; set; }

    public DateOnly AnnualRevenueThresholdEffectiveFrom { get; set; }

    public decimal IncomeBasedRequirementThreshold { get; set; }

    public DateOnly IncomeBasedRequirementEffectiveFrom { get; set; }

    public decimal SupportedRevenueCeiling { get; set; }

    public DateOnly SupportedRevenueCeilingEffectiveFrom { get; set; }

    public decimal EInvoiceRevenueThreshold { get; set; }

    public DateOnly EInvoiceRevenueThresholdEffectiveFrom { get; set; }
}
