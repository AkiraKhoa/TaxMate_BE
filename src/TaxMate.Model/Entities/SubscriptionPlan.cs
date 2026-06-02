using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TaxMate.Model.Entities;

public class SubscriptionPlan
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Precision(18,2)]
    public decimal MonthlyPrice { get; set; }

    [Precision(18,2)]
    public decimal AnnualPrice { get; set; }

    public int? MaxProducts { get; set; }

    public int? MaxTransactionsPerMonth { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public ICollection<PlanFeature> PlanFeatures { get; set; }
        = new List<PlanFeature>();

    public ICollection<UserSubscription> UserSubscriptions { get; set; }
        = new List<UserSubscription>();
}