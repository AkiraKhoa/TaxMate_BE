namespace TaxMate.Model.Common;

public static class TaxPeriodStatuses
{
    public const string Open = "Open";
    public const string Closed = "Closed";
    public const string Calculated = "Calculated";
    public const string Submitted = "Submitted";
    public const string PartiallyPaid = "PartiallyPaid";
    public const string Paid = "Paid";

    public static readonly IReadOnlyCollection<string> All =
    [
        Open,
        Closed,
        Calculated,
        Submitted,
        PartiallyPaid,
        Paid
    ];
}