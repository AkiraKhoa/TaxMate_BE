using Moq;
using System.Globalization;
using TaxMate.Model.Common;
using TaxMate.Model.Documents.Tax;
using TaxMate.Model.DTO.Expense;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Interfaces.Documents;
using TaxMate.Service.Services;
using Xunit;

namespace TaxMate.Service.Tests;

public class TaxBookServiceTests
{
    private readonly Mock<IBusinessProfileRepository> _businessProfiles = new();
    private readonly Mock<IGenericRepository<User>> _users = new();
    private readonly Mock<IGenericRepository<Income>> _incomes = new();
    private readonly Mock<IOwnerRevenueProjector> _revenueProjector = new();
    private readonly Mock<IS2bDocumentGenerator> _s2bDoc = new();
    private readonly Mock<IS2cBookProjector> _s2cProjector = new();
    private readonly Mock<IS2cDocumentGenerator> _s2cDoc = new();
    private readonly Mock<IS1aDocumentGenerator> _s1aDoc = new();
    private readonly Mock<IInventoryMovementRepository> _inventoryMovements = new();
    private readonly Mock<IS2dBookProjector> _s2dProjector = new();
    private readonly Mock<IS2dDocumentGenerator> _s2dDoc = new();
    private readonly Mock<IS2eBookProjector> _s2eProjector = new();
    private readonly Mock<IS2eDocumentGenerator> _s2eDoc = new();
    private readonly Mock<ITaxPeriodRepository> _taxPeriods = new();
    private readonly Mock<IAnnualTaxAggregateService> _annualTaxAggregate = new();
    private readonly Mock<IQttCalculationEngine> _qttEngine = new();
    private readonly Mock<IQttCalculationService> _qttCalcService = new();
    private readonly Mock<IQttDeclarationService> _qttDeclService = new();

    private readonly TaxBookService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _businessId = Guid.NewGuid();

    public TaxBookServiceTests()
    {
        _service = new TaxBookService(
            _businessProfiles.Object,
            _users.Object,
            _incomes.Object,
            _revenueProjector.Object,
            _s2bDoc.Object,
            _s2cProjector.Object,
            _s2cDoc.Object,
            _s1aDoc.Object,
            _inventoryMovements.Object,
            _s2dProjector.Object,
            _s2dDoc.Object,
            _s2eProjector.Object,
            _s2eDoc.Object,
            _taxPeriods.Object,
            _annualTaxAggregate.Object,
            _qttEngine.Object,
            _qttCalcService.Object,
            _qttDeclService.Object);
    }

    [Fact]
    public async Task GetS2bPreview_WhenAtOrBelow1B_ThrowsConflictException()
    {
        _businessProfiles.Setup(x => x.GetByIdAsync(_businessId))
            .ReturnsAsync(new BusinessProfile { Id = _businessId, OwnerId = _userId });
        _users.Setup(x => x.GetByIdAsync(_userId))
            .ReturnsAsync(new User { Id = _userId, DeclaredRevenueBracket = RevenueBrackets.AtOrBelow1B });

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.GetS2bPreviewAsync(_userId, _businessId, 2026, 1));
        Assert.Contains("Thu nhập tính thuế", ex.Message);
    }

    [Fact]
    public async Task GetS2cPreview_WhenRevenueBased_ThrowsConflictException()
    {
        _businessProfiles.Setup(x => x.GetByIdAsync(_businessId))
            .ReturnsAsync(new BusinessProfile { Id = _businessId, OwnerId = _userId });
        _users.Setup(x => x.GetByIdAsync(_userId))
            .ReturnsAsync(new User
            {
                Id = _userId,
                DeclaredRevenueBracket = RevenueBrackets.Over1BTo3B,
                PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.RevenueBased
            });

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.GetS2cPreviewAsync(_userId, _businessId, 2026, 1));
        Assert.Contains("Thu nhập tính thuế", ex.Message);
    }

    [Fact]
    public async Task GetS2dPreview_WhenServiceStore_ThrowsConflictException()
    {
        _businessProfiles.Setup(x => x.GetByIdAsync(_businessId))
            .ReturnsAsync(new BusinessProfile { Id = _businessId, OwnerId = _userId });
        _users.Setup(x => x.GetByIdAsync(_userId))
            .ReturnsAsync(new User
            {
                Id = _userId,
                DeclaredRevenueBracket = RevenueBrackets.Over1BTo3B,
                PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.IncomeBased
            });
        _businessProfiles.Setup(x => x.GetByIdWithOwnerAndCategoryAsync(_businessId))
            .ReturnsAsync(new BusinessProfile
            {
                Id = _businessId,
                OwnerId = _userId,
                MainCategoryId = Guid.Parse("d2222222-2222-2222-2222-222222222222")
            });

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.GetS2dPreviewAsync(_userId, _businessId, 2026, 1));
        Assert.Contains("dịch vụ", ex.Message);
    }

    [Fact]
    public async Task GetS2dPreview_WhenStockTrackingDisabled_ThrowsConflictException()
    {
        _businessProfiles.Setup(x => x.GetByIdAsync(_businessId))
            .ReturnsAsync(new BusinessProfile { Id = _businessId, OwnerId = _userId });
        _users.Setup(x => x.GetByIdAsync(_userId))
            .ReturnsAsync(new User
            {
                Id = _userId,
                DeclaredRevenueBracket = RevenueBrackets.Over1BTo3B,
                PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.IncomeBased
            });
        _businessProfiles.Setup(x => x.GetByIdWithOwnerAndCategoryAsync(_businessId))
            .ReturnsAsync(new BusinessProfile
            {
                Id = _businessId,
                OwnerId = _userId,
                MainCategoryId = Guid.NewGuid(),
                IsStockTrackingEnabled = false
            });

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.GetS2dPreviewAsync(_userId, _businessId, 2026, 1));
        Assert.Contains("không theo dõi kho", ex.Message);
    }

    [Fact]
    public async Task CreateQttDeclaration_WhenRevenueBased_ThrowsConflictException()
    {
        _businessProfiles.Setup(x => x.GetByIdAsync(_businessId))
            .ReturnsAsync(new BusinessProfile { Id = _businessId, OwnerId = _userId });
        _users.Setup(x => x.GetByIdAsync(_userId))
            .ReturnsAsync(new User
            {
                Id = _userId,
                DeclaredRevenueBracket = RevenueBrackets.Over1BTo3B,
                PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.RevenueBased
            });

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.CreateQttDeclarationAsync(_userId, _businessId, 2026));
        Assert.Contains("Thu nhập tính thuế", ex.Message);
    }

    [Theory]
    [InlineData("1000000000", 0, 0)]
    [InlineData("1000000001", 15, 65_957_610)]
    [InlineData("3000000000", 15, 65_957_610)]
    [InlineData("3000000001", 17, 74_751_958)]
    [InlineData("50000000000", 17, 74_751_958)]
    [InlineData("3000000000.49", 15, 65_957_610)]
    [InlineData("3000000000.5", 17, 74_751_958)]
    public async Task ExportS2c_UsesOwnerAnnualRevenueRateOnSelectedQuarterIncome(
        string annualRevenue, int expectedRate, int expectedAmount)
    {
        var book = new S2cBookProjection
        {
            BusinessId = _businessId,
            TotalRevenue = 705_525_000m,
            MaterialCost = 237_007_600m,
            PurchasedServicesCost = 28_800_000m
        };
        SetUpS2cExport(book);
        _revenueProjector.Setup(x => x.ProjectCalendarYearAsync(
                _userId, _businessId, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnnualRevenue(decimal.Parse(annualRevenue, CultureInfo.InvariantCulture)));

        S2cDocumentModel? exported = null;
        _s2cDoc.Setup(x => x.GenerateAsync(It.IsAny<S2cDocumentModel>(), It.IsAny<CancellationToken>()))
            .Callback<S2cDocumentModel, CancellationToken>((model, _) => exported = model);

        await _service.ExportS2cAsync(_userId, _businessId, 2026, 4);

        Assert.NotNull(exported);
        Assert.Equal(2026, exported.Year);
        Assert.Equal(4, exported.Quarter);
        Assert.Equal(book.TotalRevenue, exported.Revenue);
        Assert.Equal(book.TotalExpense, exported.TotalExpense);
        Assert.Equal(439_717_400m, exported.NetIncome);
        Assert.Equal((decimal)expectedRate, exported.PitRate);
        Assert.Equal((decimal)expectedAmount, exported.PitAmount);
        _revenueProjector.Verify(x => x.ProjectCalendarYearAsync(
            _userId, _businessId, 2026, It.IsAny<CancellationToken>()), Times.Once);
        _revenueProjector.Verify(x => x.ProjectBusinessAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("100", "200", 0)]
    [InlineData("10", "0", 2)]
    [InlineData("3.34", "0", 1)]
    public async Task ExportS2c_ClampsLossToZeroAndRoundsTaxAwayFromZero(
        string revenue, string cost, int expectedAmount)
    {
        var book = new S2cBookProjection
        {
            BusinessId = _businessId,
            TotalRevenue = decimal.Parse(revenue, CultureInfo.InvariantCulture),
            OtherDirectCost = decimal.Parse(cost, CultureInfo.InvariantCulture)
        };
        SetUpS2cExport(book);
        _revenueProjector.Setup(x => x.ProjectCalendarYearAsync(
                _userId, _businessId, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnnualRevenue(2_000_000_000m));

        S2cDocumentModel? exported = null;
        _s2cDoc.Setup(x => x.GenerateAsync(It.IsAny<S2cDocumentModel>(), It.IsAny<CancellationToken>()))
            .Callback<S2cDocumentModel, CancellationToken>((model, _) => exported = model);

        await _service.ExportS2cAsync(_userId, _businessId, 2026, 4);

        Assert.NotNull(exported);
        Assert.Equal(book.NetIncome, exported.NetIncome);
        Assert.Equal(15m, exported.PitRate);
        Assert.Equal((decimal)expectedAmount, exported.PitAmount);
    }

    [Fact]
    public async Task ExportS2c_ReprojectsAnnualRevenueOnEachExport()
    {
        SetUpS2cExport(new S2cBookProjection { BusinessId = _businessId, TotalRevenue = 100m });
        _revenueProjector.SetupSequence(x => x.ProjectCalendarYearAsync(
                _userId, _businessId, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnnualRevenue(2_000_000_000m))
            .ReturnsAsync(AnnualRevenue(4_000_000_000m));
        var exports = new List<S2cDocumentModel>();
        _s2cDoc.Setup(x => x.GenerateAsync(It.IsAny<S2cDocumentModel>(), It.IsAny<CancellationToken>()))
            .Callback<S2cDocumentModel, CancellationToken>((model, _) => exports.Add(model));

        await _service.ExportS2cAsync(_userId, _businessId, 2026, 4);
        await _service.ExportS2cAsync(_userId, _businessId, 2026, 4);

        Assert.Equal(2, exports.Count);
        Assert.Equal(15m, exports[0].PitRate);
        Assert.Equal(15m, exports[0].PitAmount);
        Assert.Equal(17m, exports[1].PitRate);
        Assert.Equal(17m, exports[1].PitAmount);
    }

    [Fact]
    public async Task ExportS2c_WhenAnnualInvoiceMetadataHasBlockers_UsesKnownAnnualRevenue()
    {
        SetUpS2cExport(new S2cBookProjection { BusinessId = _businessId, TotalRevenue = 100m });
        _revenueProjector.Setup(x => x.ProjectCalendarYearAsync(
                _userId, _businessId, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OwnerRevenueProjection(_userId, DateTime.MinValue, DateTime.MaxValue,
                2_000_000_000m, 0m,
                [new OwnerRevenueBlocker(OwnerRevenueBlockerCodes.MissingInvoice,
                    Guid.NewGuid(), Guid.NewGuid(), "Thiếu hóa đơn ở cửa hàng khác.")]));
        S2cDocumentModel? exported = null;
        _s2cDoc.Setup(x => x.GenerateAsync(It.IsAny<S2cDocumentModel>(), It.IsAny<CancellationToken>()))
            .Callback<S2cDocumentModel, CancellationToken>((model, _) => exported = model);

        await _service.ExportS2cAsync(_userId, _businessId, 2026, 4);

        Assert.NotNull(exported);
        Assert.Equal(15m, exported.PitRate);
        Assert.Equal(15m, exported.PitAmount);
        _s2cDoc.Verify(x => x.GenerateAsync(It.IsAny<S2cDocumentModel>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExportS2c_WhenOwnerAnnualRevenueExceedsSupportedScope_DoesNotGenerateDocument()
    {
        SetUpS2cExport(new S2cBookProjection { BusinessId = _businessId, TotalRevenue = 100m });
        _revenueProjector.Setup(x => x.ProjectCalendarYearAsync(
                _userId, _businessId, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnnualRevenue(50_000_000_001m));

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.ExportS2cAsync(_userId, _businessId, 2026, 4));

        _s2cDoc.Verify(x => x.GenerateAsync(It.IsAny<S2cDocumentModel>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetUpS2cExport(S2cBookProjection book)
    {
        _businessProfiles.Setup(x => x.GetByIdAsync(_businessId))
            .ReturnsAsync(new BusinessProfile { Id = _businessId, OwnerId = _userId });
        _users.Setup(x => x.GetByIdAsync(_userId))
            .ReturnsAsync(new User
            {
                Id = _userId,
                DeclaredRevenueBracket = RevenueBrackets.Over1BTo3B,
                PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.IncomeBased
            });
        _s2cProjector.Setup(x => x.ProjectQuarterAsync(_userId, _businessId, 2026, 4,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(book);
    }

    private OwnerRevenueProjection AnnualRevenue(decimal revenue) =>
        new(_userId, DateTime.MinValue, DateTime.MaxValue, revenue, 0m, []);
}
