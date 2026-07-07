namespace TaxMate.Model.DTO.Reports;

public class SalesTrendResponse
{
    public string Label { get; set; } = null!;

    public decimal CurrentQuarterRevenue { get; set; }

    public decimal PreviousQuarterRevenue { get; set; }
}