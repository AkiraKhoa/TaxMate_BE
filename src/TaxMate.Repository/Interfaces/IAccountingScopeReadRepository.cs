namespace TaxMate.Repository.Interfaces;

public sealed record OwnerBusinessScope(
    Guid OwnerId,
    IReadOnlySet<Guid> BusinessIds);

public sealed record RevenueTransactionSource(
    Guid BusinessId,
    Guid TransactionId,
    string TransactionType,
    string Status,
    DateTime? CompletedAt,
    decimal Amount,
    bool HasInvoice)
{
    public string? InvoiceNumber { get; init; }
    public string TransactionCode { get; init; } = string.Empty;
    public Guid? BusinessCategoryId { get; init; }
    public string? BusinessCategoryCode { get; init; }
    public string? BusinessCategoryName { get; init; }
    public decimal? VatRate { get; init; }
}

public sealed record RevenueIncomeSource(
    Guid BusinessId,
    Guid IncomeId,
    Guid? TransactionId,
    string? AccountingType,
    DateTime IncomeDate,
    decimal Amount)
{
    public string IncomeTitle { get; init; } = string.Empty;
    public Guid? BusinessCategoryId { get; init; }
    public string? BusinessCategoryCode { get; init; }
    public string? BusinessCategoryName { get; init; }
    public decimal? VatRate { get; init; }
}

public sealed record AccountingTaxPeriodSource(
    Guid TaxPeriodId,
    Guid BusinessId,
    DateTime StartNaiveUtc,
    DateTime EndExclusiveNaiveUtc,
    string Status);

public sealed record S2cExpenseSource(
    Guid ExpenseId,
    string VoucherNumber,
    DateTime ExpenseDate,
    string ExpenseTitle,
    decimal Amount,
    string CategoryName,
    string? S2cGroupCode,
    string? PaymentMethod,
    bool HasEvidence,
    bool IsInventoryPurchase);

/// <summary>
/// Read-only accounting source queries shared by mutation guards and projectors.
/// Implementations must not save changes or own transactions.
/// </summary>
public interface IAccountingScopeReadRepository
{
    Task<OwnerBusinessScope?> ResolveOwnerScopeAsync(
        Guid businessId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RevenueTransactionSource>> GetRevenueTransactionsAsync(
        IReadOnlyCollection<Guid> businessIds,
        DateTime startNaiveUtc,
        DateTime endExclusiveNaiveUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RevenueIncomeSource>> GetRevenueIncomesAsync(
        IReadOnlyCollection<Guid> businessIds,
        DateTime startNaiveUtc,
        DateTime endExclusiveNaiveUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<S2cExpenseSource>> GetS2cExpensesAsync(
        Guid businessId,
        DateTime startNaiveUtc,
        DateTime endExclusiveNaiveUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountingTaxPeriodSource>> GetTaxPeriodsIntersectingAsync(
        IReadOnlyCollection<Guid> businessIds,
        DateTime startNaiveUtc,
        DateTime endExclusiveNaiveUtc,
        CancellationToken cancellationToken = default);
}
