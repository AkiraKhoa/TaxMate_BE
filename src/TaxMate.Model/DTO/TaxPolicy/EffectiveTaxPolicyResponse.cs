namespace TaxMate.Model.DTO.TaxPolicy;

public class EffectiveTaxPolicyResponse
{
    public DateOnly EffectiveOn { get; set; }

    public decimal AnnualRevenueThreshold { get; set; }

    public DateOnly AnnualRevenueThresholdEffectiveFrom { get; set; }

    public decimal EInvoiceRevenueThreshold { get; set; }

    public DateOnly EInvoiceRevenueThresholdEffectiveFrom { get; set; }
}
