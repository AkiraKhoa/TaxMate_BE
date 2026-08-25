namespace TaxMate.Model.DTO.Tax;

public sealed class QttDeclarationResponse
{
    public Guid DeclarationId { get; init; }
    public Guid TaxPeriodId { get; init; }
    public Guid CalculationId { get; init; }
    public string DeclarationCode { get; init; } = null!;
    public int Version { get; init; }
    public int DraftRevision { get; init; }
    public string Status { get; init; } = null!;
    public string TaxpayerName { get; init; } = null!;
    public string TaxCode { get; init; } = null!;
    public QttIndicators09To24 Indicators { get; init; } = null!;
    public QttInventoryTotals31To34 InventoryTotals { get; init; } = null!;
    public IReadOnlyList<QttInventoryRow> InventoryRows { get; init; } = [];
    public QttRefundAccountSnapshot? RefundAccount { get; init; }
    public IReadOnlyList<QttOffsetItemSnapshot> OffsetItems { get; init; } = [];
}

public sealed class UpdateQttOverpaymentAllocationRequest
{
    public decimal RefundAmount { get; init; }
    public decimal OffsetAmount { get; init; }
    public Guid? RefundPaymentAccountId { get; init; }
    public IReadOnlyList<QttOffsetAllocationItemRequest> OffsetItems { get; init; } = [];
    public int ExpectedRevision { get; init; }
}

public sealed class QttOffsetAllocationItemRequest
{
    public Guid? TaxDeclarationObligationId { get; init; }
    public string? TaxCode { get; init; }
    public string? TaxpayerName { get; init; }
    public string? ObligationIdentifier { get; init; }
    public string? BudgetContent { get; init; }
    public string? ChapterCode { get; init; }
    public string? SubsectionCode { get; init; }
    public string? CollectingAuthority { get; init; }
    public string? AdministrativeAreaCode { get; init; }
    public DateTime? DueDate { get; init; }
    public decimal OutstandingAmount { get; init; }
    public decimal OffsetAmount { get; init; }
}

public sealed class ConfirmQttDeclarationRequest
{
    public int ExpectedRevision { get; init; }
}

public sealed record QttOffsetObligationOption(
    Guid ObligationId,
    string DeclarationCode,
    string TaxCode,
    string TaxpayerName,
    string BudgetContent,
    string? ChapterCode,
    string? SubsectionCode,
    string? CollectingAuthority,
    string? AdministrativeAreaCode,
    DateTime? DueDate,
    decimal OutstandingAmount);

public sealed class QttFormSnapshot
{
    public string SchemaVersion { get; init; } = null!;
    public string LegalVersion { get; init; } = null!;
    public string TemplateVersion { get; init; } = null!;
    public Guid DeclarationId { get; init; }
    public string DeclarationCode { get; init; } = null!;
    public int DeclarationVersion { get; init; }
    public int DraftRevision { get; init; }
    public Guid CalculationId { get; init; }
    public int CalculationVersion { get; init; }
    public Guid OwnerId { get; init; }
    public int TaxYear { get; init; }
    public string TaxpayerName { get; init; } = null!;
    public string TaxCode { get; init; } = null!;
    public string? TaxpayerAddress { get; init; }
    public QttIndicators09To24 Indicators { get; init; } = null!;
    public QttInventoryTotals31To34 InventoryTotals { get; init; } = null!;
    public IReadOnlyList<QttInventoryRow> InventoryRows { get; init; } = [];
    public QttRefundAccountSnapshot? RefundAccount { get; init; }
    public IReadOnlyList<QttOffsetItemSnapshot> OffsetItems { get; init; } = [];
    public QttCalculationSnapshot CalculationSnapshot { get; init; } = null!;
    public DateTime CreatedAt { get; init; }
}

public sealed record QttRefundAccountSnapshot(
    Guid PaymentAccountId,
    string AccountName,
    string AccountNumber,
    string BankName);

public sealed record QttOffsetItemSnapshot(
    Guid? SourceObligationId,
    string TaxCode,
    string TaxpayerName,
    string ObligationIdentifier,
    string BudgetContent,
    string? ChapterCode,
    string? SubsectionCode,
    string? CollectingAuthority,
    string? AdministrativeAreaCode,
    DateTime? DueDate,
    decimal OutstandingAmount,
    decimal OffsetAmount,
    decimal RemainingAmount);
