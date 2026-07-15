namespace TaxMate.Model.DTO.Reports;

public class TaxRevenueThresholdResponse
{
    public decimal Amount { get; set; }

    public decimal AccumulatedRevenue { get; set; }

    public decimal RemainingAmount { get; set; }

    public decimal ProgressPercentage { get; set; }

    public string Status { get; set; } = null!;
}