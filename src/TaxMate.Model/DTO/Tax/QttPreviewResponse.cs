namespace TaxMate.Model.DTO.Tax;

public sealed class QttPreviewResponse
{
    public Guid OwnerId { get; init; }
    public int TaxYear { get; init; }
    public string? TaxMethodSnapshot { get; init; }
    public int? TaxMethodEffectiveYear { get; init; }
    public string Eligibility { get; init; } = null!;
    public QttRevenueBreakdown Revenue { get; init; } = null!;
    public QttExpenseBreakdown Expenses { get; init; } = null!;
    public QttPitPaymentBreakdown PitPayments { get; init; } = null!;
    public QttInventorySummary Inventory { get; init; } = null!;
    public IReadOnlyList<QttCrossBookCheck> CrossBookChecks { get; init; } = [];
    public IReadOnlyList<QttPreviewIssue> Warnings { get; init; } = [];
    public IReadOnlyList<QttPreviewIssue> HardBlockers { get; init; } = [];
    public string TaxpayerName { get; init; } = string.Empty;
    public string? TaxCode { get; init; }
    public string? TaxpayerAddress { get; init; }
    public IReadOnlyList<QttBusinessScope> Businesses { get; init; } = [];
    public IReadOnlyList<QttQuarterReadiness> Quarters { get; init; } = [];
    public IReadOnlyList<QttExpenseReviewRow> ExpenseReviewRows { get; init; } = [];
    public IReadOnlyList<QttEvidenceReviewPeriod> EvidenceReviewPeriods { get; init; } = [];
    public bool CanClose =>
        HardBlockers.Count == 0 &&
        Warnings.All(x => x.Code != "EvidenceReviewRequired");
}

public sealed record QttBusinessScope(Guid BusinessId, string BusinessName);
public sealed record QttQuarterReadiness(int Quarter, Guid? TaxPeriodId, Guid? BusinessId, bool Closed);
public sealed record QttEvidenceReviewPeriod(Guid BusinessId, int Quarter, bool Required, bool Reviewed);
public sealed record QttExpenseReviewRow(
    Guid BusinessId, int Quarter, string SourceType, Guid SourceId,
    DateTime DocumentDate, string DocumentNumber, string Description,
    decimal Amount, decimal? IncludedAmount, IReadOnlyList<string> IssueCodes);

public sealed record QttRevenueBreakdown(
    decimal Indicator09a,
    decimal Indicator09b,
    decimal Indicator09c)
{
    public decimal Indicator09 => Indicator09a + Indicator09b + Indicator09c;
}

public sealed record QttExpenseBreakdown(
    decimal Indicator10a,
    decimal Indicator10b,
    decimal Indicator10c,
    decimal Indicator10d,
    decimal Indicator10LoanInterest,
    decimal Indicator10e,
    decimal ExcludedCashExpenseAmount,
    decimal ExcludedInventoryCashCost)
{
    public decimal Indicator10 =>
        Indicator10a + Indicator10b + Indicator10c + Indicator10d +
        Indicator10LoanInterest + Indicator10e;
}

public sealed class QttPitPaymentBreakdown
{
    public decimal Indicator15 { get; init; }
    public IReadOnlyList<QttPitPaymentLine> Payments { get; init; } = [];
}

public sealed record QttPitPaymentLine(
    Guid TaxPaymentId,
    string PaymentCode,
    DateTime PaymentDate,
    decimal Amount,
    string TaxType,
    string Status,
    string? SourceTaxMethod,
    bool IncludedInIndicator15);

public sealed class QttInventorySummary
{
    public decimal Indicator31OpeningValue { get; init; }
    public decimal Indicator32InboundValue { get; init; }
    public decimal Indicator33OutboundValue { get; init; }
    public decimal Indicator34EndingValue { get; init; }
    public IReadOnlyList<QttInventoryRow> Rows { get; init; } = [];
}

public sealed record QttInventoryRow(
    Guid BusinessId,
    Guid? ProductId,
    Guid? IngredientId,
    string ItemCode,
    string ItemName,
    decimal OpeningValue,
    decimal InboundValue,
    decimal OutboundValue,
    decimal EndingValue);

public sealed record QttCrossBookCheck(
    string Code,
    string Label,
    decimal ExpectedAmount,
    decimal ActualAmount,
    bool IsMatched);

public sealed record QttPreviewIssue(
    string Code,
    string Message,
    Guid? BusinessId = null,
    Guid? SourceId = null);

public static class QttEligibility
{
    public const string NormalIncomeBased = "NormalIncomeBased";
    public const string UnderOneBillionRefund = "UnderOneBillionRefund";
    public const string NotEligible = "NotEligible";
}
