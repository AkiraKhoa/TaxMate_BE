using Microsoft.Extensions.Options;
using Moq;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.TaxPolicy;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Options;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class S2aHkdExportServiceTests
{
    private readonly Mock<IBusinessProfileRepository> _businessProfiles = new();
    private readonly Mock<IS2aHkdRepository> _s2aHkdRepository = new();
    private readonly Mock<IReportRepository> _reportRepository = new();
    private readonly Mock<IOwnerRevenueProjector> _ownerRevenue = new();
    private readonly Mock<IGenericRepository<BusinessCategory>> _categories = new();
    private readonly Mock<IS2aHkdWordService> _wordService = new();
    private readonly Mock<ITaxPolicyService> _taxPolicy = new();

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _goodsCategoryId = BusinessCategoryIds.DistGoods;

    private S2aHkdExportService CreateService(decimal lower = 1_000_000_000m, decimal upper = 3_000_000_000m)
    {
        _taxPolicy
            .Setup(x => x.GetEffectiveAsync(
                It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectiveTaxPolicyResponse
            {
                AnnualRevenueThreshold = lower,
                EInvoiceRevenueThreshold = lower
            });

        return new S2aHkdExportService(
            _businessProfiles.Object,
            _s2aHkdRepository.Object,
            _reportRepository.Object,
            _categories.Object,
            _wordService.Object,
            Microsoft.Extensions.Options.Options.Create(new TaxSettings
            {
                S2aMaxRevenueThreshold = upper
            }),
            _taxPolicy.Object, _ownerRevenue.Object);
    }

    [Fact]
    public async Task BuildDocumentModelAsync_ThrowsNotEligible_WhenYtdBelowThreshold()
    {
        SetupBusiness(taxCode: "123");
        _ownerRevenue.Setup(x => x.ProjectCalendarYearAsync(_ownerId, _businessId, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OwnerRevenueProjection(_ownerId, new DateTime(2026,1,1), new DateTime(2027,1,1), 500_000_000m, 0m, []));

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            service.BuildDocumentModelAsync(_ownerId, _businessId, 2026, 1));

        Assert.Equal(S2aHkdErrorCodes.NotEligible, ex.ErrorCode);
    }

    [Fact]
    public async Task BuildDocumentModelAsync_ThrowsNotEligible_WhenYtdAboveMax()
    {
        SetupBusiness(taxCode: "123");
        _ownerRevenue.Setup(x => x.ProjectCalendarYearAsync(_ownerId, _businessId, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OwnerRevenueProjection(_ownerId, new DateTime(2026,1,1), new DateTime(2027,1,1), 3_500_000_000m, 0m, []));

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            service.BuildDocumentModelAsync(_ownerId, _businessId, 2026, 1));

        Assert.Equal(S2aHkdErrorCodes.NotEligible, ex.ErrorCode);
    }

    [Fact]
    public async Task BuildDocumentModelAsync_ThrowsMissingTaxCode()
    {
        SetupBusiness(taxCode: null);
        _ownerRevenue.Setup(x => x.ProjectCalendarYearAsync(_ownerId, _businessId, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OwnerRevenueProjection(_ownerId, new DateTime(2026,1,1), new DateTime(2027,1,1), 1_500_000_000m, 0m, []));

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            service.BuildDocumentModelAsync(_ownerId, _businessId, 2026, 1));

        Assert.Equal(S2aHkdErrorCodes.MissingTaxCode, ex.ErrorCode);
    }

    [Fact]
    public async Task BuildDocumentModelAsync_ThrowsNoRevenue_WhenQuarterEmpty()
    {
        SetupBusiness(taxCode: "123");
        _ownerRevenue.Setup(x => x.ProjectCalendarYearAsync(_ownerId, _businessId, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OwnerRevenueProjection(_ownerId, new DateTime(2026,1,1), new DateTime(2027,1,1), 1_500_000_000m, 0m, []));
        _s2aHkdRepository
            .Setup(x => x.GetProductAggregatesAsync(_businessId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            service.BuildDocumentModelAsync(_ownerId, _businessId, 2026, 1));

        Assert.Equal(S2aHkdErrorCodes.NoRevenue, ex.ErrorCode);
    }

    [Fact]
    public async Task BuildDocumentModelAsync_ThrowsMissingCategory_WhenProductUnmapped()
    {
        SetupBusiness(taxCode: "123", mainCategoryId: null);
        _ownerRevenue.Setup(x => x.ProjectCalendarYearAsync(_ownerId, _businessId, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OwnerRevenueProjection(_ownerId, new DateTime(2026,1,1), new DateTime(2027,1,1), 1_500_000_000m, 0m, []));
        _s2aHkdRepository
            .Setup(x => x.GetProductAggregatesAsync(_businessId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(
            [
                new S2aHkdProductAggregate
                {
                    ProductCode = "TM001",
                    ProductName = "Dầu ăn",
                    TotalAmount = 30_000m,
                    LastTransactionDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc)
                }
            ]);
        _categories
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync([]);

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            service.BuildDocumentModelAsync(_ownerId, _businessId, 2026, 1));

        Assert.Equal(S2aHkdErrorCodes.MissingCategory, ex.ErrorCode);
    }

    [Fact]
    public async Task BuildDocumentModelAsync_ReturnsGoldenSampleTotals()
    {
        SetupBusiness(taxCode: "12345566", mainCategoryId: _goodsCategoryId);
        _ownerRevenue.Setup(x => x.ProjectCalendarYearAsync(_ownerId, _businessId, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OwnerRevenueProjection(_ownerId, new DateTime(2026,1,1), new DateTime(2027,1,1), 1_500_000_000m, 0m, []));
        _s2aHkdRepository
            .Setup(x => x.GetProductAggregatesAsync(_businessId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(
            [
                new S2aHkdProductAggregate
                {
                    ProductCode = "TM001",
                    ProductName = "Dầu ăn",
                    ProductBusinessCategoryId = _goodsCategoryId,
                    TotalAmount = 30_000m,
                    LastTransactionDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc)
                },
                new S2aHkdProductAggregate
                {
                    ProductCode = "TM002",
                    ProductName = "Nước mắm",
                    ProductBusinessCategoryId = _goodsCategoryId,
                    TotalAmount = 40_000m,
                    LastTransactionDate = new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc)
                },
                new S2aHkdProductAggregate
                {
                    ProductCode = "CK001",
                    ProductName = "Giặt sấy",
                    ProductBusinessCategoryId = BusinessCategoryIds.ServiceConstruct,
                    TotalAmount = 100_000m,
                    LastTransactionDate = new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc)
                },
                new S2aHkdProductAggregate
                {
                    ProductCode = "CK002",
                    ProductName = "Giặt hấp",
                    ProductBusinessCategoryId = BusinessCategoryIds.ServiceConstruct,
                    TotalAmount = 500_000m,
                    LastTransactionDate = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc)
                }
            ]);
        _categories
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(
            [
                new BusinessCategory
                {
                    BusinessCategoryId = _goodsCategoryId,
                    Code = "DIST_GOODS",
                    Name = "Bán tạp hóa",
                    VatRate = 1m,
                    PitRate = 0.5m
                },
                new BusinessCategory
                {
                    BusinessCategoryId = BusinessCategoryIds.ServiceConstruct,
                    Code = "SERVICE_CONSTRUCT",
                    Name = "Giặt là",
                    VatRate = 5m,
                    PitRate = 2m
                }
            ]);

        var service = CreateService();
        var model = await service.BuildDocumentModelAsync(_ownerId, _businessId, 2026, 1);

        Assert.Equal(2, model.Groups.Count);
        Assert.Equal(30_700m, model.Footer.TotalVatTax);
        Assert.Equal(12_350m, model.Footer.TotalPitTax);

        var goods = model.Groups.Single(x => x.CategoryCode == "DIST_GOODS");
        Assert.Equal(70_000m, goods.Subtotal);
        Assert.Equal(700m, goods.VatTax);
        Assert.Equal(350m, goods.PitTax);

        var serviceGroup = model.Groups.Single(x => x.CategoryCode == "SERVICE_CONSTRUCT");
        Assert.Equal(600_000m, serviceGroup.Subtotal);
        Assert.Equal(30_000m, serviceGroup.VatTax);
        Assert.Equal(12_000m, serviceGroup.PitTax);
    }

    [Fact]
    public async Task BuildDocumentModelAsync_ThrowsUnauthorized_WhenNotOwner()
    {
        SetupBusiness(taxCode: "123", ownerId: Guid.NewGuid());
        var service = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.BuildDocumentModelAsync(_ownerId, _businessId, 2026, 1));
    }

    private void SetupBusiness(string? taxCode, Guid? mainCategoryId = null, Guid? ownerId = null)
    {
        var business = new BusinessProfile
        {
            Id = _businessId,
            OwnerId = ownerId ?? _ownerId,
            BusinessName = "ABC",
            Address = "44a Vườn Lài",
            MainCategoryId = mainCategoryId,
            Owner = new User
            {
                Id = ownerId ?? _ownerId,
                TaxCode = taxCode,
                Email = "test@example.com",
                FullName = "Test"
            }
        };

        _businessProfiles
            .Setup(x => x.GetByIdWithOwnerAndCategoryAsync(_businessId))
            .ReturnsAsync(business);
    }
}
