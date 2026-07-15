namespace TaxMate.Model.DTO.Reports;

public class CashFlowDashboardResponse
{
    public CashFlowPeriodResponse Period { get; set; } = null!;
    public CashFlowSummaryResponse Summary { get; set; } = null!;
    public List<ExpenseDistributionResponse> ExpenseDistribution { get; set; } = [];
    public List<CashFlowTrendResponse> CashFlowTrend { get; set; } = [];
}