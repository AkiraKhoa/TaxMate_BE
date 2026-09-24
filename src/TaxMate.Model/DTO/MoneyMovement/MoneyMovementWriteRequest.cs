namespace TaxMate.Model.DTO.MoneyMovement;

public sealed class MoneyMovementWriteRequest
{
    public Guid OwnerId { get; init; }
    public Guid BusinessId { get; init; }
    public Guid PaymentAccountId { get; init; }
    public string PaymentMethod { get; init; } = null!;
    public string MovementType { get; init; } = null!;
    public decimal Amount { get; init; }
    public DateTime MovementDate { get; init; }
    public string DocumentNumber { get; init; } = null!;
    public string Description { get; init; } = null!;
    public Guid ReferenceId { get; init; }
}
