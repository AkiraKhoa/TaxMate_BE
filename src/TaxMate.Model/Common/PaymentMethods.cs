namespace TaxMate.Model.Common;

public static class PaymentMethods
{
    public const string Cash = "Cash";
    public const string Transfer = "Transfer";

    public static readonly IReadOnlyCollection<string> All =
    [
        Cash,
        Transfer
    ];
}
