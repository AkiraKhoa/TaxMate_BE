namespace TaxMate.Model.Common;

public static class IncomeAccountingTypes
{
    public const string BusinessRevenue = "BusinessRevenue";
    public const string NonRevenueCashIn = "NonRevenueCashIn";

    public static readonly IReadOnlyCollection<string> All =
    [
        BusinessRevenue,
        NonRevenueCashIn
    ];
}
