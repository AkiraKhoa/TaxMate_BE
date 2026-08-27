using System.Linq.Expressions;
using Moq;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.TaxPolicy;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class TaxPolicyServiceTests
{
    private readonly Mock<IGenericRepository<TaxThresholdSetting>> _thresholds =
        new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    [Fact]
    public async Task GetEffectiveThresholdAsync_ReturnsLatestVersionNotAfterDate()
    {
        _thresholds
            .Setup(x => x.FindAsync(
                It.IsAny<Expression<Func<TaxThresholdSetting, bool>>>()))
            .ReturnsAsync(
            [
                Setting(new DateOnly(2026, 1, 1), 1_000_000_000m),
                Setting(new DateOnly(2026, 7, 1), 1_200_000_000m)
            ]);

        var result = await CreateService().GetEffectiveThresholdAsync(
            TaxThresholdTypes.AnnualRevenueTax,
            new DateOnly(2026, 8, 20));

        Assert.Equal(new DateOnly(2026, 7, 1), result.EffectiveFrom);
        Assert.Equal(1_200_000_000m, result.Amount);
    }

    [Fact]
    public async Task GetEffectiveThresholdAsync_UsesEarliestVersionBeforeFirstDate()
    {
        _thresholds
            .SetupSequence(x => x.FindAsync(
                It.IsAny<Expression<Func<TaxThresholdSetting, bool>>>()))
            .ReturnsAsync([])
            .ReturnsAsync(
            [
                Setting(new DateOnly(2027, 1, 1), 1_500_000_000m),
                Setting(new DateOnly(2026, 1, 1), 1_000_000_000m)
            ]);

        var result = await CreateService().GetEffectiveThresholdAsync(
            TaxThresholdTypes.AnnualRevenueTax,
            new DateOnly(2025, 12, 31));

        Assert.Equal(new DateOnly(2026, 1, 1), result.EffectiveFrom);
    }

    [Fact]
    public async Task UpsertAsync_CreatesIndependentVersionForTypeAndDate()
    {
        _thresholds
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<TaxThresholdSetting, bool>>>()))
            .ReturnsAsync((TaxThresholdSetting?)null);
        _unitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var adminId = Guid.NewGuid();
        var effectiveFrom = new DateOnly(2026, 7, 1);
        var result = await CreateService().UpsertAsync(
            TaxThresholdTypes.EInvoiceRequirement,
            new UpdateTaxThresholdSettingRequest
            {
                Amount = 1_500_000_000m,
                EffectiveFrom = effectiveFrom
            },
            adminId);

        _thresholds.Verify(x => x.AddAsync(It.Is<TaxThresholdSetting>(setting =>
            setting.Type == TaxThresholdTypes.EInvoiceRequirement &&
            setting.Amount == 1_500_000_000m &&
            setting.EffectiveFrom == effectiveFrom &&
            setting.UpdatedByUserId == adminId)), Times.Once);
        Assert.Equal(effectiveFrom, result.EffectiveFrom);
    }

    [Fact]
    public async Task UpsertAsync_RejectsNonPositiveThreshold()
    {
        await Assert.ThrowsAsync<BadRequestException>(() =>
            CreateService().UpsertAsync(
                TaxThresholdTypes.AnnualRevenueTax,
                new UpdateTaxThresholdSettingRequest
                {
                    Amount = 0,
                    EffectiveFrom = new DateOnly(2026, 1, 1)
                },
                Guid.NewGuid()));
    }

    private TaxPolicyService CreateService() => new(
        _thresholds.Object,
        _unitOfWork.Object);

    private static TaxThresholdSetting Setting(
        DateOnly effectiveFrom,
        decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        Type = TaxThresholdTypes.AnnualRevenueTax,
        Amount = amount,
        EffectiveFrom = effectiveFrom,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
