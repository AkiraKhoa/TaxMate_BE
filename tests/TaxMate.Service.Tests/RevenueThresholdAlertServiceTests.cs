using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TaxMate.Model.DTO.Reports;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Options;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class RevenueThresholdAlertServiceTests
{
    private readonly Mock<IGenericRepository<BusinessProfile>> _businessProfiles = new();
    private readonly Mock<IGenericRepository<RevenueThresholdAlert>> _alerts = new();
    private readonly Mock<IReportRepository> _reports = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _businessId = Guid.NewGuid();

    private RevenueThresholdAlertService CreateService(decimal threshold = 1_000_000_000m)
    {
        return new RevenueThresholdAlertService(
            _businessProfiles.Object,
            _alerts.Object,
            _reports.Object,
            _users.Object,
            _email.Object,
            _unitOfWork.Object,
            Microsoft.Extensions.Options.Options.Create(new TaxSettings { BusinessRevenueThreshold = threshold }),
            NullLogger<RevenueThresholdAlertService>.Instance);
    }

    [Fact]
    public async Task CheckAfterSaleAsync_DoesNotEmail_WhenTotalBelowThreshold()
    {
        SetupBusiness();
        SetupNotYetSent();
        SetupProfiles(400_000_000m, 500_000_000m);

        await CreateService().CheckAfterSaleAsync(_businessId);

        _email.Verify(
            x => x.SendRevenueThresholdEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<decimal>(),
                It.IsAny<IReadOnlyList<OwnerProfileRevenueRow>>(),
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _alerts.Verify(x => x.AddAsync(It.IsAny<RevenueThresholdAlert>()), Times.Never);
    }

    [Fact]
    public async Task CheckAfterSaleAsync_SendsOnce_WhenFourPeriodTotalCrossesThreshold()
    {
        SetupBusiness();
        SetupNotYetSent();
        SetupOwner();
        var profiles = SetupProfiles(600_000_000m, 500_000_000m);

        await CreateService().CheckAfterSaleAsync(_businessId);

        _alerts.Verify(x => x.AddAsync(It.Is<RevenueThresholdAlert>(alert =>
            alert.OwnerId == _ownerId &&
            alert.TotalRevenue == 1_100_000_000m)), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _email.Verify(
            x => x.SendRevenueThresholdEmailAsync(
                "owner@example.com",
                "Chủ hộ",
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                1_000_000_000m,
                profiles,
                1_100_000_000m,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckAfterSaleAsync_Skips_WhenAlreadySentThisYear()
    {
        SetupBusiness();
        _alerts
            .Setup(x => x.AnyAsync(It.IsAny<Expression<Func<RevenueThresholdAlert, bool>>>()))
            .ReturnsAsync(true);

        await CreateService().CheckAfterSaleAsync(_businessId);

        _reports.Verify(
            x => x.GetOwnerRevenueByProfileAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _email.Verify(
            x => x.SendRevenueThresholdEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<decimal>(),
                It.IsAny<IReadOnlyList<OwnerProfileRevenueRow>>(),
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckAfterSaleAsync_RemovesAlertAndDoesNotThrow_WhenSmtpFails()
    {
        SetupBusiness();
        SetupNotYetSent();
        SetupOwner();
        SetupProfiles(1_000_000_000m);
        _email
            .Setup(x => x.SendRevenueThresholdEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<decimal>(),
                It.IsAny<IReadOnlyList<OwnerProfileRevenueRow>>(),
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));

        var ex = await Record.ExceptionAsync(() =>
            CreateService().CheckAfterSaleAsync(_businessId));

        Assert.Null(ex);
        _alerts.Verify(x => x.Remove(It.IsAny<RevenueThresholdAlert>()), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CheckAfterSaleAsync_DoesNotThrow_WhenBusinessMissing()
    {
        _businessProfiles
            .Setup(x => x.GetByIdAsync(_businessId))
            .ReturnsAsync((BusinessProfile?)null);

        var ex = await Record.ExceptionAsync(() =>
            CreateService().CheckAfterSaleAsync(_businessId));

        Assert.Null(ex);
        _email.Verify(
            x => x.SendRevenueThresholdEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<decimal>(),
                It.IsAny<IReadOnlyList<OwnerProfileRevenueRow>>(),
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void SetupBusiness()
    {
        _businessProfiles
            .Setup(x => x.GetByIdAsync(_businessId))
            .ReturnsAsync(new BusinessProfile
            {
                Id = _businessId,
                OwnerId = _ownerId,
                BusinessName = "Shop A",
                IsActive = true
            });
    }

    private void SetupNotYetSent()
    {
        _alerts
            .Setup(x => x.AnyAsync(It.IsAny<Expression<Func<RevenueThresholdAlert, bool>>>()))
            .ReturnsAsync(false);
        _alerts
            .Setup(x => x.AddAsync(It.IsAny<RevenueThresholdAlert>()))
            .Returns(Task.CompletedTask);
        _unitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private void SetupOwner()
    {
        _users
            .Setup(x => x.GetByIdAsync(_ownerId))
            .ReturnsAsync(new User
            {
                Id = _ownerId,
                Email = "owner@example.com",
                FullName = "Chủ hộ"
            });
    }

    private List<OwnerProfileRevenueRow> SetupProfiles(params decimal[] amounts)
    {
        var rows = amounts
            .Select((amount, index) => new OwnerProfileRevenueRow
            {
                BusinessId = Guid.NewGuid(),
                BusinessName = $"Shop {index + 1}",
                Revenue = amount
            })
            .ToList();

        _reports
            .Setup(x => x.GetOwnerRevenueByProfileAsync(
                _ownerId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        return rows;
    }
}
