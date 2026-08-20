namespace TaxMate.Model.DTO.TaxPolicy;

public class UpdateTaxThresholdSettingRequest
{
    public decimal Amount { get; set; }

    public DateOnly EffectiveFrom { get; set; }
}
