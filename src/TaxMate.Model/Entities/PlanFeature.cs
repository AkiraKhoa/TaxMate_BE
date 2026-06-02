using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.Entities;

public class PlanFeature
{
    public Guid Id { get; set; }

    public Guid SubscriptionPlanId { get; set; }

    [Required]
    [MaxLength(100)]
    public string FeatureKey { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string FeatureName { get; set; } = null!;

    public bool IsEnabled { get; set; }

    public SubscriptionPlan SubscriptionPlan { get; set; } = null!;
}