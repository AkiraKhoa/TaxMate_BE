using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TaxMate.Model.DTO.Reports;
using TaxMate.Model.DTO.TaxPolicy;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class RevenueThresholdAlertServiceTests
{
    private readonly Mock<IOwnerRevenueProjector> _revenue = new();
    private readonly Mock<IGenericRepository<BusinessProfile>> _businesses = new();
    private readonly Mock<IGenericRepository<RevenueThresholdAlert>> _alerts = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ITaxPolicyService> _policy = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly List<RevenueThresholdAlert> _storedAlerts = [];
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _businessId = Guid.NewGuid();

    public RevenueThresholdAlertServiceTests()
    {
        _businesses.Setup(x => x.GetByIdAsync(_businessId)).ReturnsAsync(
            new BusinessProfile { Id = _businessId, OwnerId = _ownerId });
        _alerts.Setup(x => x.FindAsync(
                It.IsAny<Expression<Func<RevenueThresholdAlert, bool>>>()))
            .ReturnsAsync((Expression<Func<RevenueThresholdAlert, bool>> predicate) =>
                _storedAlerts.Where(predicate.Compile()).ToList());
        _alerts.Setup(x => x.AddAsync(It.IsAny<RevenueThresholdAlert>()))
            .Callback<RevenueThresholdAlert>(_storedAlerts.Add)
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _policy.Setup(x => x.GetEffectiveAsync(
                It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectiveTaxPolicyResponse
            {
                AnnualRevenueThreshold = 1_000_000_000m,
                IncomeBasedRequirementThreshold = 3_000_000_000m,
                SupportedRevenueCeiling = 50_000_000_000m
            });
    }

    [Fact]
    public async Task EvaluateAsync_DoesNotCreateAlert_AtExactThreshold()
    {
        SetupProjection(1_000_000_000m);

        var result = await CreateService().EvaluateAsync(
            _ownerId, _businessId, 2026);

        Assert.Empty(result);
        _alerts.Verify(x => x.AddAsync(It.IsAny<RevenueThresholdAlert>()),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_CreatesIndependentAlerts_ForCrossedThresholds()
    {
        SetupProjection(3_000_000_001m);

        var result = await CreateService().EvaluateAsync(
            _ownerId, _businessId, 2026);

        Assert.Equal(2, result.Count);
        Assert.Collection(result,
            x => Assert.Equal("Crossed1B", x.ThresholdCode),
            x => Assert.Equal("Crossed3B", x.ThresholdCode));
    }

    [Fact]
    public async Task EvaluateAsync_KeepsAlert_WhenEmailFails()
    {
        SetupProjection(1_000_000_001m);
        _users.Setup(x => x.GetByIdAsync(_ownerId)).ReturnsAsync(new User
        {
            Id = _ownerId,
            Email = "owner@example.com",
            FullName = "Chủ hộ"
        });
        _email.Setup(x => x.SendRevenueThresholdEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<decimal>(),
                It.IsAny<IReadOnlyList<OwnerProfileRevenueRow>>(),
                It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));

        var error = await Record.ExceptionAsync(() => CreateService()
            .EvaluateAsync(_ownerId, _businessId, 2026));

        Assert.Null(error);
        Assert.Single(_storedAlerts);
        _alerts.Verify(x => x.Remove(It.IsAny<RevenueThresholdAlert>()),
            Times.Never);
    }

    private RevenueThresholdAlertService CreateService() => new(
        _revenue.Object,
        _businesses.Object,
        _alerts.Object,
        _users.Object,
        _policy.Object,
        _email.Object,
        _unitOfWork.Object,
        NullLogger<RevenueThresholdAlertService>.Instance);

    private void SetupProjection(decimal revenue)
    {
        var start = new DateTime(2025, 12, 31, 17, 0, 0);
        var end = new DateTime(2026, 12, 31, 17, 0, 0);
        _revenue.Setup(x => x.ProjectCalendarYearAsync(
                _ownerId, _businessId, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OwnerRevenueProjection(
                _ownerId, start, end, revenue, 0m, [])
            {
                Lines =
                [
                    new OwnerRevenueLine(
                        Guid.NewGuid(), "SALE", Guid.NewGuid(), "Sale",
                        "BH-1", new DateTime(2026, 8, 1), "Doanh thu", revenue)
                ]
            });
    }
}
