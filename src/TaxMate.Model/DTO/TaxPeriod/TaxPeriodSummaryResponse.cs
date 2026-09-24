namespace TaxMate.Model.DTO.TaxPeriod;

public class TaxPeriodSummaryResponse
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    public string PeriodType { get; set; } = null!;

    public int Year { get; set; }

    public int? Month { get; set; }

    public int? Quarter { get; set; }

    public string? FilingWindow { get; set; }

    public DateTime PeriodStartDate { get; set; }

    public DateTime PeriodEndDate { get; set; }

    public DateTime? DueDate { get; set; }

    public string Status { get; set; } = null!;

    public decimal TotalRevenue { get; set; }

    public decimal TaxableRevenue { get; set; }

    public decimal EstimatedTax { get; set; }

    public decimal TaxAmountDebt { get; set; }

    public DateTime? PaidDate { get; set; }
}
