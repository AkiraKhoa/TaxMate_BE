namespace TaxMate.Model.DTO.Reports;

public class SalesDashboardSummaryResponse
{
    public decimal TotalRevenue { get; set; }

    public int TotalOrders { get; set; }

    public decimal TotalProductsSold { get; set; }
}