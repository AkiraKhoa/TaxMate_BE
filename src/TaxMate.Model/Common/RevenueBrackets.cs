namespace TaxMate.Model.Common;

public static class RevenueBrackets
{
    public const string AtOrBelow1B = "AtOrBelow1B";
    public const string Over1BTo3B = "Over1BTo3B";
    public const string Over3BTo50B = "Over3BTo50B";

    public static readonly IReadOnlyCollection<string> All =
    [
        AtOrBelow1B,
        Over1BTo3B,
        Over3BTo50B
    ];
}
