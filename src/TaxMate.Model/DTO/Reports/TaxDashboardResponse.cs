namespace TaxMate.Model.DTO.Reports;

public class TaxDashboardResponse
{
    public int Year { get; set; }

    public TaxRevenueThresholdResponse Threshold { get; set; } = null!;

    public TaxRevenueForecastResponse Forecast { get; set; } = null!;

    public List<TaxQuarterRevenueResponse> Quarters { get; set; } = [];
}