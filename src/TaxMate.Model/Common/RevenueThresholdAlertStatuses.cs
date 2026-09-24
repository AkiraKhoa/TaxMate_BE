namespace TaxMate.Model.Common;

public static class RevenueThresholdAlertStatuses
{
    public const string PendingReview = "PendingReview";
    public const string Acknowledged = "Acknowledged";
    public const string Resolved = "Resolved";

    public static readonly IReadOnlyCollection<string> All =
    [
        PendingReview,
        Acknowledged,
        Resolved
    ];
}
