using TaxMate.Model.Common;
using TaxMate.Model.DTO.MoneyMovement;
using TaxMate.Model.Entities;
using TaxMate.Service.Common;

namespace TaxMate.Service.Services;

public static class S2eBalanceCalculator
{
    public static S2eBalanceCalculation Calculate(
        decimal initialBalance,
        DateTime initialBalanceDate,
        DateTime fromInclusive,
        DateTime toExclusive,
        IEnumerable<MoneyMovement> movements)
    {
        initialBalanceDate = BangkokBusinessTime.NormalizeNaiveUtc(
            initialBalanceDate);
        fromInclusive = BangkokBusinessTime.NormalizeNaiveUtc(fromInclusive);
        toExclusive = BangkokBusinessTime.NormalizeNaiveUtc(toExclusive);

        if (fromInclusive >= toExclusive)
        {
            throw new ArgumentException("The S2e date range must be a non-empty half-open range.");
        }

        if (initialBalanceDate > fromInclusive)
        {
            throw new ArgumentException("The initial balance must be effective on or before the report start.");
        }

        var opening = initialBalance;
        var totalIn = 0m;
        var totalOut = 0m;

        var normalizedMovements = movements
            .Select(x => new
            {
                Movement = x,
                MovementDate = BangkokBusinessTime.NormalizeNaiveUtc(
                    x.MovementDate)
            });
        foreach (var item in normalizedMovements
                     .Where(x => x.MovementDate >= initialBalanceDate && x.MovementDate < toExclusive)
                     .OrderBy(x => x.MovementDate)
                     .ThenBy(x => x.Movement.CreatedAt)
                     .ThenBy(x => x.Movement.MoneyMovementId))
        {
            var movement = item.Movement;
            var isIn = IsInflow(movement.MovementType);
            if (item.MovementDate < fromInclusive)
            {
                opening += isIn ? movement.Amount : -movement.Amount;
                continue;
            }

            if (isIn)
            {
                totalIn += movement.Amount;
            }
            else
            {
                totalOut += movement.Amount;
            }
        }

        return new S2eBalanceCalculation
        {
            OpeningBalance = opening,
            TotalIn = totalIn,
            TotalOut = totalOut,
            EndingBalance = opening + totalIn - totalOut
        };
    }

    public static bool IsInflow(string movementType)
        => movementType switch
        {
            MoneyMovementTypes.PaymentIn => true,
            MoneyMovementTypes.ManualIncomeIn => true,
            MoneyMovementTypes.ExpenseOut => false,
            _ => throw new ArgumentOutOfRangeException(
                nameof(movementType),
                movementType,
                "Unsupported money movement type.")
        };
}
