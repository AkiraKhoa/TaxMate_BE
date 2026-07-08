namespace TaxMate.Model.DTO.Reports;

public class ProductRevenueDistributionResponse
{
    public string ProductName { get; set; } = null!;

    public decimal Revenue { get; set; }

    public decimal Percentage { get; set; }
}