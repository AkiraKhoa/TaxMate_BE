namespace TaxMate.Model.DTO.MoneyMovement;

public sealed class S2eBalanceCalculation
{
    public decimal OpeningBalance { get; init; }
    public decimal TotalIn { get; init; }
    public decimal TotalOut { get; init; }
    public decimal EndingBalance { get; init; }
}
