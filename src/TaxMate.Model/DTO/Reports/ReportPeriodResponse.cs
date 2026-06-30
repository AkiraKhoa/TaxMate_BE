namespace TaxMate.Model.DTO.Reports;

public class ReportPeriodResponse
{
    public int Year { get; set; }

    public int Month { get; set; }

    public string Label { get; set; } = null!;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }
}