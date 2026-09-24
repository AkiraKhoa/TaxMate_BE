namespace TaxMate.Model.Common;

public static class MoneyMovementTypes
{
    public const string PaymentIn = "PaymentIn";
    public const string ManualIncomeIn = "ManualIncomeIn";
    public const string ExpenseOut = "ExpenseOut";

    public static readonly IReadOnlyCollection<string> All =
    [
        PaymentIn,
        ManualIncomeIn,
        ExpenseOut
    ];
}
