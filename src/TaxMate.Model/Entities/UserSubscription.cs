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
    public string Status { get; set; } = "PendingPayment";

    [Required]
    [MaxLength(20)]
    public string BillingCycle { get; set; } = "Monthly";

    public bool AutoRenew { get; set; }

    public long? PaymentOrderCode { get; set; }

    [MaxLength(200)]
    public string? PaymentLinkId { get; set; }

    [MaxLength(1000)]
    public string? CheckoutUrl { get; set; }

    [Required]
    [MaxLength(50)]
    public string PaymentStatus { get; set; } = "Pending";

    public User User { get; set; } = null!;

    public SubscriptionPlan SubscriptionPlan { get; set; } = null!;
}