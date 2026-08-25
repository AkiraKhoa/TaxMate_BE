namespace TaxMate.Model.Common;

public static class S2cGroupCodes
{
    public const string Labor = "Labor";
    public const string PurchasedServices = "PurchasedServices";
    public const string OtherDirect = "OtherDirect";

    public static readonly IReadOnlyCollection<string> All =
    [
        Labor,
        PurchasedServices,
        OtherDirect
    ];
}
