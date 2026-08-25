namespace TaxMate.Model.Common;

public static class InventoryMovementTypes
{
    public const string OpeningBalance = "OpeningBalance";
    public const string PurchaseIn = "PurchaseIn";
    public const string OrderOut = "OrderOut";
    public const string AdjustmentIn = "AdjustmentIn";
    public const string AdjustmentOut = "AdjustmentOut";

    public static readonly IReadOnlyCollection<string> All =
    [
        OpeningBalance,
        PurchaseIn,
        OrderOut,
        AdjustmentIn,
        AdjustmentOut
    ];
}
