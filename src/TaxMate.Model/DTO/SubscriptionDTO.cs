namespace TaxMate.Model.DTO;

public class PlanFeatureResponse
{
    public Guid Id { get; set; }
    public string FeatureKey { get; set; } = null!;
    public string FeatureName { get; set; } = null!;
    public bool IsEnabled { get; set; }
}

public class SubscriptionPlanResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal MonthlyPrice { get; set; }
    public decimal AnnualPrice { get; set; }
    public int? MaxProducts { get; set; }
    public int? MaxTransactionsPerMonth { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public List<PlanFeatureResponse> Features { get; set; } = new();
}

public class UserSubscriptionResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserFullName { get; set; } = null!;
    public Guid SubscriptionPlanId { get; set; }
    public string SubscriptionPlanName { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = null!;
    public string BillingCycle { get; set; } = null!;
    public bool AutoRenew { get; set; }
    public string PaymentStatus { get; set; } = null!;
    public string? CheckoutUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SubscribeRequest
{
    public Guid SubscriptionPlanId { get; set; }
    public string BillingCycle { get; set; } = "Monthly"; // "Monthly" or "Annual"
    public bool AutoRenew { get; set; }
}

public class SubscribeResponse
{
    public Guid SubscriptionId { get; set; }
    public Guid SubscriptionPlanId { get; set; }
    public string PlanName { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Status { get; set; } = null!;
    public string PaymentStatus { get; set; } = null!;
    public string CheckoutUrl { get; set; } = null!;
    public long OrderCode { get; set; }
}
