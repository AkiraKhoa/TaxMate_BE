namespace TaxMate.Model.DTO.Reports;

public class EstimatedProfitSummaryResponse
{
    public decimal Profit { get; set; }

    public decimal Revenue { get; set; }

    public decimal CostOfGoodsSold { get; set; }
}