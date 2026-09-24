namespace TaxMate.Service.Interfaces;

public static class OwnerRevenueBlockerCodes
{
    public const string MissingInvoice = "MissingInvoice";
    public const string MissingBusinessCategory = "MissingBusinessCategory";
    public const string NonPositiveManualRevenue = "NonPositiveManualRevenue";
}

public sealed record OwnerRevenueBlocker(
    string Code,
    Guid BusinessId,
    Guid SourceId,
    string Message);

public sealed record OwnerRevenueGroup(
    Guid BusinessCategoryId,
    string BusinessCategoryCode,
    string BusinessCategoryName,
    decimal VatRate,
    decimal CompletedTransactionRevenue,
    decimal ManualBusinessRevenue)
{
    public decimal TotalRevenue =>
        CompletedTransactionRevenue + ManualBusinessRevenue;

    public decimal VatAmount => TotalRevenue * VatRate / 100m;
}

public sealed record OwnerRevenueLine(
    Guid BusinessCategoryId,
    string BusinessCategoryCode,
    Guid SourceId,
    string SourceType,
    string DocumentNumber,
    DateTime DocumentDate,
    string Description,
    decimal Amount);

public sealed record OwnerRevenueProjection(
    Guid OwnerId,
    DateTime StartNaiveUtc,
    DateTime EndExclusiveNaiveUtc,
    decimal CompletedTransactionRevenue,
    decimal ManualBusinessRevenue,
    IReadOnlyList<OwnerRevenueBlocker> Blockers)
{
    public IReadOnlyList<OwnerRevenueGroup> Groups { get; init; } = [];
    public IReadOnlyList<OwnerRevenueLine> Lines { get; init; } = [];

    public decimal TotalRevenue =>
        CompletedTransactionRevenue + ManualBusinessRevenue;

    public bool IsValid => Blockers.Count == 0;
}

public interface IOwnerRevenueProjector
{
    Task<OwnerRevenueProjection> ProjectCalendarYearAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default);

    Task<OwnerRevenueProjection> ProjectAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        DateTime startNaiveUtc,
        DateTime endExclusiveNaiveUtc,
        CancellationToken cancellationToken = default);

    Task<OwnerRevenueProjection> ProjectBusinessAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        DateTime startNaiveUtc,
        DateTime endExclusiveNaiveUtc,
        CancellationToken cancellationToken = default);
}
