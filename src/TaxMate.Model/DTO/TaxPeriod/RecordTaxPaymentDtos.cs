namespace TaxMate.Model.DTO.TaxPeriod;

public sealed record RecordTaxPaymentItemRequest(
    string TaxType,
    decimal Amount,
    string? StateBudgetChapterCode = null,
    string? StateBudgetSubsectionCode = null,
    string? AdministrativeAreaCode = null);

public sealed record RecordTaxPeriodPaymentRequest(
    DateTime PaymentDate,
    string PaymentMethod = "Bank",
    string? TransactionReference = null,
    string? ReceiptFileUrl = null,
    string? Note = null,
    IReadOnlyList<RecordTaxPaymentItemRequest>? Items = null);

public sealed record TaxPaymentDetailResponse(
    Guid Id,
    string TaxType,
    string PaymentCode,
    decimal Amount,
    DateTime PaymentDate,
    string PaymentMethod,
    string Status,
    string? TransactionReference,
    string? StateBudgetChapterCode,
    string? StateBudgetSubsectionCode,
    string? AdministrativeAreaCode,
    string? ReceiptFileUrl,
    string? Note);

public sealed record TaxPeriodPaymentSummaryResponse(
    Guid TaxPeriodId,
    string PeriodStatus,
    DateTime? PaidDate,
    decimal TotalPaidAmount,
    IReadOnlyList<TaxPaymentDetailResponse> Payments);
