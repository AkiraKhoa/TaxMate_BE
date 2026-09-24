namespace TaxMate.Model.Common;

public static class TaxPeriodTypes
{
    public const string Monthly = "Monthly";
    public const string Quarterly = "Quarterly";
    public const string Yearly = "Yearly";
    public const string Tkn = "Tkn";

    public static readonly IReadOnlyCollection<string> All =
    [
        Monthly,
        Quarterly,
        Yearly,
        Tkn
    ];
}
