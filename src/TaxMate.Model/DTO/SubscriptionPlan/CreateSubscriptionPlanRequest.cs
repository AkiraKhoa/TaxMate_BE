using TaxMate.Model.DTO.PlanFeature;

namespace TaxMate.Model.DTO.SubscriptionPlan;

public class CreateSubscriptionPlanRequest
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal MonthlyPrice { get; set; }

    public decimal AnnualPrice { get; set; }

    public int? MaxProducts { get; set; }

    public int? MaxTransactionsPerMonth { get; set; }

    public int SortOrder { get; set; }

    public List<CreatePlanFeatureRequest> Features { get; set; } = new();
}