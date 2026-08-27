namespace TaxMate.Model.Common;

public static class PaymentAccountTypes
{
    public const string Cash = "Cash";
    public const string Bank = "Bank";

    public static readonly IReadOnlyCollection<string> All =
    [
        Cash,
        Bank
    ];
}
