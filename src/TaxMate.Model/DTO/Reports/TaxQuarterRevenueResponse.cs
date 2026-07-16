namespace TaxMate.Model.DTO.Reports;

public class TaxQuarterRevenueResponse
{
    public int Quarter { get; set; }

    public decimal Revenue { get; set; }

    public string Status { get; set; } = null!;
}