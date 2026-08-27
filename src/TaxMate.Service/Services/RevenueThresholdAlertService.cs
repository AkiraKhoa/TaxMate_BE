using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaxMate.Model.Common;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public sealed class RevenueThresholdAlertService
    : IRevenueThresholdAlertService
{
    private readonly IOwnerRevenueProjector _ownerRevenue;
    private readonly IGenericRepository<BusinessProfile> _businesses;
    private readonly IGenericRepository<RevenueThresholdAlert> _alerts;
    private readonly IUserRepository _users;
    private readonly ITaxPolicyService _policies;
    private readonly IEmailService _email;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RevenueThresholdAlertService> _logger;

    public RevenueThresholdAlertService(
        IOwnerRevenueProjector ownerRevenue,
        IGenericRepository<BusinessProfile> businesses,
        IGenericRepository<RevenueThresholdAlert> alerts,
        IUserRepository users,
        ITaxPolicyService policies,
        IEmailService email,
        IUnitOfWork unitOfWork,
        ILogger<RevenueThresholdAlertService> logger)
    {
        _ownerRevenue = ownerRevenue;
        _businesses = businesses;
        _alerts = alerts;
        _users = users;
        _policies = policies;
        _email = email;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RevenueThresholdAlert>> EvaluateAsync(
        Guid ownerId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default)
    {
        if (year is < 2000 or > 9998)
            throw new BadRequestException("Revenue year is invalid.");

        var business = await _businesses.GetByIdAsync(businessId)
            ?? throw new NotFoundException("Business profile not found.");
        if (business.OwnerId != ownerId)
            throw new ForbiddenException();

        var projection = await _ownerRevenue.ProjectCalendarYearAsync(
            ownerId, businessId, year, cancellationToken);
        var policy = await _policies.GetEffectiveAsync(
            new DateOnly(year, 12, 31), cancellationToken);
        var existing = (await _alerts.FindAsync(x =>
                x.OwnerId == ownerId && x.Year == year))
            .ToDictionary(x => x.ThresholdCode, StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        var newlyPending = new List<RevenueThresholdAlert>();

        foreach (var threshold in GetThresholds(policy))
        {
            existing.TryGetValue(threshold.Code, out var alert);
            var isExceeded = projection.TotalRevenue > threshold.Amount;

            if (!isExceeded)
            {
                if (alert?.Status == RevenueThresholdAlertStatuses.PendingReview)
                {
                    alert.Status = RevenueThresholdAlertStatuses.Acknowledged;
                    alert.TotalRevenue = projection.TotalRevenue;
                    alert.WindowEnd = projection.EndExclusiveNaiveUtc;
                    alert.ResolvedAt = null;
                    alert.UpdatedAt = now;
                    _alerts.Update(alert);
                }
                continue;
            }

            if (alert?.Status == RevenueThresholdAlertStatuses.Resolved)
                continue;

            // Acknowledged while still above means a deferred/unsupported
            // transition already seen by the user. Auto-acknowledged alerts
            // below the threshold store the lower total and can reopen.
            if (alert?.Status == RevenueThresholdAlertStatuses.Acknowledged &&
                alert.TotalRevenue > threshold.Amount)
            {
                continue;
            }

            var crossing = FindCrossing(projection, threshold.Amount);
            if (crossing is null)
                continue;

            if (alert is null)
            {
                alert = new RevenueThresholdAlert
                {
                    Id = Guid.NewGuid(),
                    OwnerId = ownerId,
                    Year = year,
                    CreatedAt = now
                };
                await _alerts.AddAsync(alert);
                existing[threshold.Code] = alert;
            }
            else
            {
                _alerts.Update(alert);
            }

            alert.Quarter = QuarterOf(crossing.Value.Date);
            alert.WindowStart = projection.StartNaiveUtc;
            alert.WindowEnd = crossing.Value.Date;
            alert.TotalRevenue = crossing.Value.CumulativeRevenue;
            alert.SentAt = now;
            alert.ThresholdCode = threshold.Code;
            alert.ThresholdAmount = threshold.Amount;
            alert.Status = RevenueThresholdAlertStatuses.PendingReview;
            alert.ResolvedAt = null;
            alert.UpdatedAt = now;
            newlyPending.Add(alert);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex,
                "Concurrent threshold evaluation for owner {OwnerId}, year {Year}.",
                ownerId, year);
        }

        await NotifyAsync(ownerId, newlyPending, cancellationToken);
        return (await _alerts.FindAsync(x =>
                x.OwnerId == ownerId && x.Year == year))
            .OrderBy(x => x.ThresholdAmount)
            .ToList();
    }

    private async Task NotifyAsync(
        Guid ownerId,
        IReadOnlyList<RevenueThresholdAlert> alerts,
        CancellationToken cancellationToken)
    {
        if (alerts.Count == 0)
            return;
        var owner = await _users.GetByIdAsync(ownerId);
        if (owner is null || string.IsNullOrWhiteSpace(owner.Email))
            return;

        foreach (var alert in alerts)
        {
            try
            {
                await _email.SendRevenueThresholdEmailAsync(
                    owner.Email,
                    owner.FullName,
                    alert.Year,
                    alert.Quarter,
                    alert.WindowStart,
                    alert.WindowEnd,
                    alert.ThresholdAmount,
                    [],
                    alert.TotalRevenue,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to notify threshold {ThresholdCode} for owner {OwnerId}.",
                    alert.ThresholdCode, ownerId);
            }
        }
    }

    private static IReadOnlyList<ThresholdDefinition> GetThresholds(
        TaxMate.Model.DTO.TaxPolicy.EffectiveTaxPolicyResponse policy) =>
    [
        new(RevenueThresholdCodes.Crossed1B,
            policy.AnnualRevenueThreshold),
        new(RevenueThresholdCodes.Crossed3B,
            policy.IncomeBasedRequirementThreshold),
        new(RevenueThresholdCodes.Crossed50B,
            policy.SupportedRevenueCeiling)
    ];

    private static (DateTime Date, decimal CumulativeRevenue)? FindCrossing(
        OwnerRevenueProjection projection,
        decimal threshold)
    {
        decimal cumulative = 0m;
        foreach (var line in projection.Lines
                     .OrderBy(x => x.DocumentDate)
                     .ThenBy(x => x.SourceType)
                     .ThenBy(x => x.SourceId))
        {
            cumulative += line.Amount;
            if (cumulative > threshold)
                return (line.DocumentDate, cumulative);
        }

        // A source with incomplete document metadata can still contribute to the
        // legal annual total even when it cannot be represented as a book line.
        return projection.TotalRevenue > threshold
            ? (projection.EndExclusiveNaiveUtc.AddTicks(-1),
                projection.TotalRevenue)
            : null;
    }

    private static int QuarterOf(DateTime naiveUtc)
    {
        var month = BangkokBusinessTime
            .NaiveUtcToBangkokWallClock(naiveUtc).Month;
        return ((month - 1) / 3) + 1;
    }

    private sealed record ThresholdDefinition(string Code, decimal Amount);
}
