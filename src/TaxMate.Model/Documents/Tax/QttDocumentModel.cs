using TaxMate.Model.DTO.Tax;

namespace TaxMate.Model.Documents.Tax;

public sealed class QttDocumentModel
{
    public QttFormSnapshot Snapshot { get; init; } = null!;
    public DateTime ExportDate { get; init; }
    public IReadOnlyList<QttPaymentSupportDocumentRow> PaymentSupportRows { get; init; } = [];
}

public sealed record QttPaymentSupportDocumentRow(
    string BudgetContent,
    decimal Amount,
    string? ChapterCode,
    string? SubsectionCode,
    string? AdministrativeAreaCode,
    string? CollectingAuthority,
    string? TaxAuthority,
    DateTime? DueDate);
