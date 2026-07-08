namespace TaxMate.Model.DTO.Reports;

public class ActiveSalesMonthResponse
{
    public int Year { get; set; }

    public int Month { get; set; }

    public string Label { get; set; } = null!;

    public int TotalOrders { get; set; }

    public decimal TotalRevenue { get; set; }
}