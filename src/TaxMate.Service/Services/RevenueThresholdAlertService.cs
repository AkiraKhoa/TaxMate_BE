using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaxMate.Model.Common;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Options;

namespace TaxMate.Service.Services;

public class RevenueThresholdAlertService : IRevenueThresholdAlertService
{
    private readonly IGenericRepository<BusinessProfile> _businessProfiles;
    private readonly IGenericRepository<RevenueThresholdAlert> _alerts;
    private readonly IReportRepository _reports;
    private readonly IUserRepository _users;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TaxSettings _taxSettings;
    private readonly ILogger<RevenueThresholdAlertService> _logger;

    public RevenueThresholdAlertService(
        IGenericRepository<BusinessProfile> businessProfiles,
        IGenericRepository<RevenueThresholdAlert> alerts,
        IReportRepository reports,
        IUserRepository users,
        IEmailService emailService,
        IUnitOfWork unitOfWork,
        IOptions<TaxSettings> taxSettings,
        ILogger<RevenueThresholdAlertService> logger)
    {
        _businessProfiles = businessProfiles;
        _alerts = alerts;
        _reports = reports;
        _users = users;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
        _taxSettings = taxSettings.Value;
        _logger = logger;
    }

    public async Task CheckAfterSaleAsync(
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await CheckAfterSaleCoreAsync(businessId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to check 1-tỷ revenue threshold after sale for business {BusinessId}.",
                businessId);
        }
    }

    private async Task CheckAfterSaleCoreAsync(
        Guid businessId,
        CancellationToken cancellationToken)
    {
        var business = await _businessProfiles.GetByIdAsync(businessId);
        if (business == null)
        {
            return;
        }

        var asOfUtc = DateTime.UtcNow;
        var (windowStart, windowEnd, currentYear, currentQuarter) =
            TaxPeriodWindow.GetCurrentAndPreviousThreeQuarterWindow(asOfUtc);

        var alreadySent = await _alerts.AnyAsync(alert =>
            alert.OwnerId == business.OwnerId && alert.Year == currentYear);
        if (alreadySent)
        {
            return;
        }

        var profiles = await _reports.GetOwnerRevenueByProfileAsync(
            business.OwnerId,
            windowStart,
            windowEnd,
            cancellationToken);
        var total = profiles.Sum(row => row.Revenue);
        if (total < _taxSettings.BusinessRevenueThreshold)
        {
            return;
        }

        var owner = await _users.GetByIdAsync(business.OwnerId);
        if (owner == null || string.IsNullOrWhiteSpace(owner.Email))
        {
            return;
        }

        var alert = new RevenueThresholdAlert
        {
            Id = Guid.NewGuid(),
            OwnerId = business.OwnerId,
            Year = currentYear,
            Quarter = currentQuarter,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            TotalRevenue = total,
            SentAt = asOfUtc,
            CreatedAt = asOfUtc,
            UpdatedAt = asOfUtc
        };

        await _alerts.AddAsync(alert);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return;
        }

        try
        {
            await _emailService.SendRevenueThresholdEmailAsync(
                owner.Email,
                owner.FullName,
                currentYear,
                currentQuarter,
                windowStart,
                windowEnd,
                _taxSettings.BusinessRevenueThreshold,
                profiles,
                total,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send 1-tỷ revenue threshold email to owner {OwnerId} for year {Year}.",
                business.OwnerId,
                currentYear);

            _alerts.Remove(alert);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
