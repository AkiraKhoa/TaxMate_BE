namespace TaxMate.Model.Common;

public static class RevenueThresholdCodes
{
    public const string Crossed1B = "Crossed1B";
    public const string Crossed3B = "Crossed3B";
    public const string Crossed50B = "Crossed50B";

    public static readonly IReadOnlyCollection<string> All =
    [
        Crossed1B,
        Crossed3B,
        Crossed50B
    ];
}
