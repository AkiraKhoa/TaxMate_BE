namespace TaxMate.Model.DTO.TaxPeriod;

public class TaxPeriodDetailResponse
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

    public decimal SalesRevenue { get; set; }

    public decimal OtherRevenue { get; set; }

    public decimal TotalRevenue { get; set; }

    public decimal TaxableRevenue { get; set; }

    public decimal VatTaxAmount { get; set; }

    public decimal PersonalIncomeTaxAmount { get; set; }

    public decimal EstimatedTax { get; set; }

    public decimal TaxAmountDebt { get; set; }

    public decimal TotalExpense { get; set; }

    public decimal EstimatedProfit { get; set; }

    public int TransactionCount { get; set; }

    public int PaidTransactionCount { get; set; }

    public int UnpaidTransactionCount { get; set; }

    public int MissingInvoiceCount { get; set; }

    public int ExpenseCount { get; set; }

    public string DataCheckStatus { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime? DueDate { get; set; }

    public DateTime? PaidDate { get; set; }

    public DateTime? ClosedAt { get; set; }

    public DateTime? CalculatedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }
}
