namespace TaxMate.Model.DTO.TaxPeriod;

public class CloseTaxPeriodResponse
{
    public Guid TaxPeriodId { get; set; }

    public string Status { get; set; } = null!;

    public decimal SalesRevenue { get; set; }

    public decimal OtherRevenue { get; set; }

    public decimal TotalRevenue { get; set; }

    public decimal TaxableRevenue { get; set; }

    public DateTime ClosedAt { get; set; }
}