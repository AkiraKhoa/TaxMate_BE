namespace TaxMate.Model.DTO.Reports;

public class EstimatedProfitTrendResponse
{
    public int Month { get; set; }

    public string Label { get; set; } = null!;

    public decimal Profit { get; set; }
}