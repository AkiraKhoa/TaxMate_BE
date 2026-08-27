namespace TaxMate.Model.DTO.MoneyMovement;

public sealed class MoneyMovementSourceAuditRecord
{
    public string MovementType { get; init; } = null!;
    public Guid ReferenceId { get; init; }
    public decimal Amount { get; init; }
    public DateTime MovementDate { get; init; }
    public Guid? PaymentAccountId { get; init; }
}
