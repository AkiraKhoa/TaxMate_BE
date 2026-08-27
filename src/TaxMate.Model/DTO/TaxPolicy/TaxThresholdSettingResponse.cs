namespace TaxMate.Model.DTO.TaxPolicy;

public class TaxThresholdSettingResponse
{
    public Guid Id { get; set; }

    public string Type { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
