namespace TaxMate.Model.DTO.Reports;

public class SalesDashboardResponse
{
    public ReportPeriodResponse Period { get; set; } = null!;

    public SalesDashboardSummaryResponse Summary { get; set; } = null!;

    public List<ProductRevenueDistributionResponse> RevenueDistribution { get; set; } = [];

    public List<TopSellingProductResponse> TopSellingProducts { get; set; } = [];

    public List<SalesTrendResponse> SalesTrend { get; set; } = [];
}