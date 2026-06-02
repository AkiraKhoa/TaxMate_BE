using System.ComponentModel.DataAnnotations;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class UserSubscription : BaseEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid SubscriptionPlanId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Active";

    [Required]
    [MaxLength(20)]
    public string BillingCycle { get; set; } = "Monthly";

    public bool AutoRenew { get; set; }

    public User User { get; set; } = null!;

    public SubscriptionPlan SubscriptionPlan { get; set; } = null!;
}