using Moq;
using TaxMate.Model.Common;
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
}
