namespace TaxMate.Model.DTO.TaxPeriod;

public class TaxPeriodPreviewResponse
{
    public Guid TaxPeriodId { get; set; }

    public Guid BusinessId { get; set; }

    public string Status { get; set; } = null!;

    public decimal SalesRevenue { get; set; }

    public decimal OtherRevenue { get; set; }

    public decimal TotalRevenue { get; set; }

    public decimal TaxableRevenue { get; set; }

    public decimal TotalExpense { get; set; }

    public int TransactionCount { get; set; }

    public int CompletedTransactionCount { get; set; }

    public int UnpaidTransactionCount { get; set; }

    public int CancelledTransactionCount { get; set; }

    public int MissingInvoiceCount { get; set; }

    public int ExpenseCount { get; set; }

    public string DataCheckStatus { get; set; } = null!;

    public bool CanClose { get; set; }

    public List<TaxPeriodWarningResponse> Warnings { get; set; } = [];
}

public class TaxPeriodWarningResponse
{
    public string Code { get; set; } = null!;

    public string Message { get; set; } = null!;
}