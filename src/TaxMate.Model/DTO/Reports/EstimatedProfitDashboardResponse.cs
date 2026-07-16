namespace TaxMate.Model.DTO.Reports;

public class EstimatedProfitDashboardResponse
{
    public EstimatedProfitPeriodResponse Period { get; set; } = null!;

    public EstimatedProfitSummaryResponse Summary { get; set; } = null!;

    public List<EstimatedProfitTrendResponse> ProfitTrend { get; set; } = [];
}