namespace TaxMate.Model.DTO.Reports;

public class CashFlowPeriodResponse
{
    public int Year { get; set; }
    public int Quarter { get; set; }
    public string Label { get; set; } = null!;
    public int StartMonth { get; set; }
    public int EndMonth { get; set; }
}