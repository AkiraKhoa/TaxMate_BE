namespace TaxMate.Model.DTO.Expense;

public sealed class S2cBookProjection
{
    public Guid BusinessId { get; init; }
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEndExclusive { get; init; }
    public decimal TotalRevenue { get; init; }
    public decimal MaterialCost { get; init; }
    public decimal LaborCost { get; init; }
    public decimal PurchasedServicesCost { get; init; }
    public decimal OtherDirectCost { get; init; }
    public int ExcludedCashPaymentExpenseCount { get; init; }
    public decimal ExcludedCashPaymentExpenseAmount { get; init; }
    public decimal ExcludedInventoryCashCost { get; init; }
    public DateTime? EvidenceReviewedAt { get; init; }
    public Guid? EvidenceReviewedByUserId { get; init; }
    public decimal TotalExpense =>
        MaterialCost + LaborCost + PurchasedServicesCost + OtherDirectCost;
    public decimal NetIncome => TotalRevenue - TotalExpense;
    public IReadOnlyList<S2cExpenseLine> Lines { get; init; } = [];
    public IReadOnlyList<S2cBookWarning> Warnings { get; init; } = [];
    public bool IsReady => Warnings.Count == 0;
}

public sealed record S2cExpenseLine(
    Guid ExpenseId,
    string VoucherNumber,
    DateTime ExpenseDate,
    string ExpenseTitle,
    string CategoryName,
    string GroupCode,
    decimal Amount,
    bool HasEvidence);

public sealed record S2cBookWarning(
    string Code,
    string Message,
    Guid? SourceId = null,
    bool CanOverride = false);
