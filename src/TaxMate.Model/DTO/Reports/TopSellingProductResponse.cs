namespace TaxMate.Model.DTO.Reports;

public class TopSellingProductResponse
{
    public int Rank { get; set; }

    public string ProductName { get; set; } = null!;

    public decimal QuantitySold { get; set; }

    public decimal Revenue { get; set; }
}