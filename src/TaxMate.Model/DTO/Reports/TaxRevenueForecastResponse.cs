namespace TaxMate.Model.DTO.Reports;

public class TaxRevenueForecastResponse
{
    public decimal EstimatedYearEndRevenue { get; set; }

    public int BasedOnThroughQuarter { get; set; }

    public string Label { get; set; } = null!;
}