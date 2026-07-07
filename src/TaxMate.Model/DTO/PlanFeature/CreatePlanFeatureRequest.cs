namespace TaxMate.Model.DTO.PlanFeature;

public class CreatePlanFeatureRequest
{
    public string FeatureKey { get; set; } = null!;

    public string FeatureName { get; set; } = null!;

    public bool IsEnabled { get; set; }
}