namespace TaxMate.Model.DTO.MoneyMovement;

public enum MoneyMovementWriteOutcome
{
    Created,
    Updated,
    Unchanged
}

public sealed class MoneyMovementWriteResult
{
    public Guid MoneyMovementId { get; init; }
    public MoneyMovementWriteOutcome Outcome { get; init; }
}
