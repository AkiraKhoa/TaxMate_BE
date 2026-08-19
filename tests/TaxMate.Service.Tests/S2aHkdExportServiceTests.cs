using Microsoft.Extensions.Options;
using Moq;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.Reports;
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
    private readonly Mock<IGenericRepository<BusinessCategory>> _categories = new();
    private readonly Mock<IS2aHkdWordService> _wordService = new();

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _goodsCategoryId = BusinessCategoryIds.DistGoods;

    private S2aHkdExportService CreateService(decimal lower = 1_000_000_000m, decimal upper = 3_000_000_000m)
    {
        return new S2aHkdExportService(
            _businessProfiles.Object,
            _s2aHkdRepository.Object,
            _reportRepository.Object,
            _categories.Object,
            _wordService.Object,
            Microsoft.Extensions.Options.Options.Create(new TaxSettings
            {
                BusinessRevenueThreshold = lower,
                S2aMaxRevenueThreshold = upper
            }));
    }

    [Fact]
    public async Task BuildDocumentModelAsync_ThrowsNotEligible_WhenYtdBelowThreshold()
    {
        SetupBusiness(taxCode: "123", ytdRevenue: 500_000_000m);

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            service.BuildDocumentModelAsync(_ownerId, _businessId, 2026, 1));

        Assert.Equal(S2aHkdErrorCodes.NotEligible, ex.ErrorCode);
    }

    [Fact]
    public async Task BuildDocumentModelAsync_ThrowsNotEligible_WhenYtdAboveMax()
    {
        SetupBusiness(taxCode: "123", ytdRevenue: 3_500_000_000m);

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            service.BuildDocumentModelAsync(_ownerId, _businessId, 2026, 1));

        Assert.Equal(S2aHkdErrorCodes.NotEligible, ex.ErrorCode);
    }

    [Fact]
    public async Task BuildDocumentModelAsync_ThrowsMissingTaxCode()
    {
        SetupBusiness(taxCode: null, ytdRevenue: 1_500_000_000m);

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            service.BuildDocumentModelAsync(_ownerId, _businessId, 2026, 1));

        Assert.Equal(S2aHkdErrorCodes.MissingTaxCode, ex.ErrorCode);
    }

    [Fact]
    public async Task BuildDocumentModelAsync_ThrowsNoRevenue_WhenQuarterEmpty()
    {
        SetupBusiness(taxCode: "123", ytdRevenue: 1_500_000_000m);
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
        SetupBusiness(taxCode: "123", ytdRevenue: 1_500_000_000m, mainCategoryId: null);
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
        SetupBusiness(taxCode: "12345566", ytdRevenue: 1_500_000_000m, mainCategoryId: _goodsCategoryId);
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
        SetupCategories();

        var service = CreateService();
        var models = await service.BuildDocumentModelAsync(_ownerId, _businessId, 2026, 1);
        var model = Assert.Single(models);

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
    public async Task BuildDocumentModelAsync_CombinesAllOwnerBusinesses()
    {
        var secondBusinessId = Guid.NewGuid();
        var first = CreateBusiness(_businessId, "Cua hang A", "12345566", _goodsCategoryId);
        var second = CreateBusiness(secondBusinessId, "Cua hang B", "12345566", _goodsCategoryId);

        _businessProfiles
            .Setup(x => x.GetByIdWithOwnerAndCategoryAsync(_businessId))
            .ReturnsAsync(first);
        _businessProfiles
            .Setup(x => x.GetActiveByOwnerWithOwnerAndCategoryAsync(_ownerId))
            .ReturnsAsync([first, second]);
        SetupOwnerYtd(1_500_000_000m, first.Id, second.Id);
        SetupCategories();

        _s2aHkdRepository
            .Setup(x => x.GetProductAggregatesAsync(_businessId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(
            [
                new S2aHkdProductAggregate
                {
                    ProductCode = "TM001",
                    ProductName = "Dầu ăn",
                    ProductBusinessCategoryId = _goodsCategoryId,
                    TotalAmount = 70_000m,
                    LastTransactionDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc)
                }
            ]);
        _s2aHkdRepository
            .Setup(x => x.GetProductAggregatesAsync(secondBusinessId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(
            [
                new S2aHkdProductAggregate
                {
                    ProductCode = "CK001",
                    ProductName = "Giặt sấy",
                    ProductBusinessCategoryId = BusinessCategoryIds.ServiceConstruct,
                    TotalAmount = 600_000m,
                    LastTransactionDate = new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc)
                }
            ]);

        var service = CreateService();
        var models = await service.BuildDocumentModelAsync(_ownerId, _businessId, 2026, 1);

        Assert.Equal(2, models.Count);
        Assert.Equal("Cua hang A", models[0].Header.BusinessName);
        Assert.Equal(700m, models[0].Footer.TotalVatTax);
        Assert.Equal(350m, models[0].Footer.TotalPitTax);
        Assert.Equal("Cua hang B", models[1].Header.BusinessName);
        Assert.Equal(30_000m, models[1].Footer.TotalVatTax);
        Assert.Equal(12_000m, models[1].Footer.TotalPitTax);
    }

    [Fact]
    public async Task BuildDocumentModelAsync_ThrowsUnauthorized_WhenNotOwner()
    {
        SetupBusiness(taxCode: "123", ytdRevenue: 1_500_000_000m, ownerId: Guid.NewGuid());
        var service = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.BuildDocumentModelAsync(_ownerId, _businessId, 2026, 1));
    }

    private void SetupBusiness(
        string? taxCode,
        decimal ytdRevenue,
        Guid? mainCategoryId = null,
        Guid? ownerId = null)
    {
        var business = CreateBusiness(_businessId, "ABC", taxCode, mainCategoryId, ownerId);

        _businessProfiles
            .Setup(x => x.GetByIdWithOwnerAndCategoryAsync(_businessId))
            .ReturnsAsync(business);
        _businessProfiles
            .Setup(x => x.GetActiveByOwnerWithOwnerAndCategoryAsync(business.OwnerId))
            .ReturnsAsync([business]);
        _categories
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync([]);
        SetupOwnerYtd(ytdRevenue, business.Id);
    }

    private void SetupOwnerYtd(decimal revenue, params Guid[] businessIds)
    {
        var rows = businessIds.Select(id => new OwnerProfileRevenueRow
        {
            BusinessId = id,
            BusinessName = "Shop",
            Revenue = businessIds.Length == 0 ? 0 : revenue / businessIds.Length
        }).ToList();

        if (rows.Count == 1)
            rows[0].Revenue = revenue;

        _reportRepository
            .Setup(x => x.GetOwnerRevenueByProfileAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);
    }

    private void SetupCategories()
    {
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
    }

    private BusinessProfile CreateBusiness(
        Guid id,
        string name,
        string? taxCode,
        Guid? mainCategoryId = null,
        Guid? ownerId = null)
    {
        var resolvedOwnerId = ownerId ?? _ownerId;
        return new BusinessProfile
        {
            Id = id,
            OwnerId = resolvedOwnerId,
            BusinessName = name,
            Address = "44a Vườn Lài",
            MainCategoryId = mainCategoryId,
            IsActive = true,
            Owner = new User
            {
                Id = resolvedOwnerId,
                TaxCode = taxCode,
                Email = "test@example.com",
                FullName = "Test"
            }
        };
    }
}
