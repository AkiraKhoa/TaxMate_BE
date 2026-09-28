using Moq;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class InventoryControlWorkflowTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IInventoryControlRepository> _controls = new();
    private readonly Mock<IInventoryMovementService> _movements = new();
    private readonly Mock<ITaxPeriodMutationGuard> _guard = new();

    [Fact]
    public async Task Initialize_RejectsDuplicateOpeningAndRollsBack()
    {
        var (ownerId, business) = SetupOwnedBusiness();
        _controls.Setup(x => x.GetMovementsAsync(
                business.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Movement(business.Id, Guid.NewGuid(), 1m)]);

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateInitializationService().InitializeAsync(
                ownerId,
                business.Id,
                OpeningRequest(),
                CancellationToken.None));

        _movements.Verify(x => x.StageOpeningBalancesAsync(
            It.IsAny<StageInventoryOpeningBalancesCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Initialize_LockedPeriodDoesNotReadOrWriteInventory()
    {
        var ownerId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        _guard.Setup(x => x.EnsureCanCreateAsync(
                ownerId, businessId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("locked"));

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateInitializationService().InitializeAsync(
                ownerId,
                businessId,
                OpeningRequest(),
                CancellationToken.None));

        _controls.Verify(x => x.GetMovementsAsync(
            It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Initialize_StagesOneOpeningForEachPositiveConfirmedItem()
    {
        var (ownerId, business) = SetupOwnedBusiness();
        var product = Product(business.Id, stock: 8m);
        var ingredient = Ingredient(business.Id, stock: 4m);
        SetupActiveItems(business.Id, [product], [ingredient]);
        _controls.Setup(x => x.GetMovementsAsync(
                business.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        StageInventoryOpeningBalancesCommand? staged = null;
        _movements.Setup(x => x.StageOpeningBalancesAsync(
                It.IsAny<StageInventoryOpeningBalancesCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback<StageInventoryOpeningBalancesCommand, CancellationToken>(
                (command, _) => staged = command)
            .ReturnsAsync((StageInventoryOpeningBalancesCommand command, CancellationToken _) =>
                command.Lines.Select(line => new InventoryMovement
                {
                    InventoryMovementId = Guid.NewGuid(),
                    BusinessId = command.BusinessId,
                    ProductId = line.ProductId,
                    IngredientId = line.IngredientId,
                    MovementType = InventoryMovementTypes.OpeningBalance,
                    Quantity = line.Quantity,
                    TotalValue = line.TotalValue
                }).ToArray());

        var result = await CreateInitializationService().InitializeAsync(
            ownerId,
            business.Id,
            new InitializeInventoryRequest
            {
                OccurredAt = new DateTime(2026, 1, 1),
                DocumentNumber = "OPEN-01",
                Description = "Xác nhận tồn đầu",
                Lines =
                [
                    new()
                    {
                        ProductId = product.Id,
                        Quantity = 3m,
                        TotalValue = 30m
                    },
                    new()
                    {
                        IngredientId = ingredient.Id,
                        Quantity = 2m,
                        TotalValue = 50m
                    }
                ]
            });

        Assert.NotNull(staged);
        Assert.Equal(2, staged.Lines.Count);
        Assert.Equal(2, result.OpeningBalanceCount);
        Assert.Equal(3m, product.StockQuantity);
        Assert.Equal(10m, product.CostPrice);
        Assert.Equal(2m, ingredient.StockQuantity);
        Assert.Equal(25m, ingredient.EstimatedPrice);
        _unitOfWork.Verify(x => x.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reconcile_UsesLedgerNotCacheAndEnablesTrackingAtomically()
    {
        var (ownerId, business) = SetupOwnedBusiness(enabled: false);
        var product = Product(business.Id, stock: 999m);
        var ingredient = Ingredient(business.Id, stock: 999m);
        _controls.Setup(x => x.GetActiveProductsAsync(
                business.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([product]);
        _controls.Setup(x => x.GetActiveIngredientsAsync(
                business.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ingredient]);
        _controls.Setup(x => x.GetMovementsAsync(
                business.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Movement(business.Id, product.Id, 10m),
                Movement(business.Id, product.Id, 3m, InventoryMovementTypes.OrderOut),
                IngredientMovement(business.Id, ingredient.Id, 5m)
            ]);
        var staged = new List<StageInventoryAdjustmentCommand>();
        _movements.Setup(x => x.StageAdjustmentAsync(
                It.IsAny<StageInventoryAdjustmentCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback<StageInventoryAdjustmentCommand, CancellationToken>(
                (command, _) => staged.Add(command))
            .ReturnsAsync((StageInventoryAdjustmentCommand command, CancellationToken _) =>
                new InventoryMovement
                {
                    InventoryMovementId = Guid.NewGuid(),
                    BusinessId = command.BusinessId,
                    ProductId = command.ProductId,
                    IngredientId = command.IngredientId,
                    MovementType = command.MovementType,
                    Quantity = command.Quantity
                });

        var result = await CreateAdjustmentService().ReconcileAsync(
            ownerId,
            business.Id,
            new ReconcileInventoryRequest
            {
                ExpectedVersion = await CurrentVersion(business.Id),
                OccurredAt = new DateTime(2026, 8, 20),
                DocumentNumber = "KK-01",
                Description = "Kiểm kho bật lại",
                Lines =
                [
                    new()
                    {
                        ProductId = product.Id,
                        ActualQuantity = 9m,
                        AdjustmentInTotalValue = 20m
                    },
                    new()
                    {
                        IngredientId = ingredient.Id,
                        ActualQuantity = 2m
                    }
                ]
            },
            enableStockTracking: true);

        Assert.True(business.IsStockTrackingEnabled);
        Assert.All(staged, x => Assert.InRange(x.OccurredAt,
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1)));
        Assert.Equal(9m, product.StockQuantity);
        Assert.Equal(2m, ingredient.StockQuantity);
        Assert.Equal(1, result.AdjustmentInCount);
        Assert.Equal(1, result.AdjustmentOutCount);
        Assert.Contains(staged, x =>
            x.ProductId == product.Id &&
            x.MovementType == InventoryMovementTypes.AdjustmentIn &&
            x.Quantity == 2m &&
            x.TotalValue == 20m);
        Assert.Contains(staged, x =>
            x.IngredientId == ingredient.Id &&
            x.MovementType == InventoryMovementTypes.AdjustmentOut &&
            x.Quantity == 3m &&
            x.TotalValue is null);
        _unitOfWork.Verify(x => x.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reconcile_AdjustmentInWithoutValueIsRejected()
    {
        var (ownerId, business) = SetupOwnedBusiness(enabled: false);
        var product = Product(business.Id, stock: 1m);
        SetupActiveItems(business.Id, [product], []);
        _controls.Setup(x => x.GetMovementsAsync(
                business.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Movement(business.Id, product.Id, 1m)]);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            CreateAdjustmentService().ReconcileAsync(
                ownerId,
                business.Id,
                Reconcile(product.Id, actual: 2m, value: null, version: CurrentVersion(business.Id).GetAwaiter().GetResult()),
                enableStockTracking: true));

        Assert.Contains("giá trị điều chỉnh tăng", exception.Message);
        Assert.False(business.IsStockTrackingEnabled);
        _unitOfWork.Verify(x => x.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reconcile_NegativeActualQuantityIsRejected()
    {
        var (ownerId, business) = SetupOwnedBusiness();
        var product = Product(business.Id, stock: 1m);
        SetupActiveItems(business.Id, [product], []);
        _controls.Setup(x => x.GetMovementsAsync(
                business.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Movement(business.Id, product.Id, 1m)]);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            CreateAdjustmentService().ReconcileAsync(
                ownerId,
                business.Id,
                Reconcile(product.Id, actual: -1m, value: null, version: CurrentVersion(business.Id).GetAwaiter().GetResult()),
                enableStockTracking: false));

        _movements.Verify(x => x.StageAdjustmentAsync(
            It.IsAny<StageInventoryAdjustmentCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Preview_CrossOwnerIsForbidden()
    {
        var (_, business) = SetupOwnedBusiness();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateInitializationService().GetPreviewAsync(
                Guid.NewGuid(), business.Id));
    }

    [Fact]
    public async Task ToggleOff_OnlyChangesVisibilityFlagAndDoesNotReconcile()
    {
        var ownerId = Guid.NewGuid();
        var business = Business(ownerId, enabled: true);
        var businessProfiles = new Mock<IBusinessProfileRepository>();
        businessProfiles.Setup(x => x.GetByIdWithCategoryAsync(business.Id))
            .ReturnsAsync(business);
        var adjustments = new Mock<IInventoryAdjustmentService>();
        var service = new BusinessProfileService(
            _unitOfWork.Object,
            businessProfiles.Object,
            new Mock<IGenericRepository<User>>().Object,
            new Mock<IGenericRepository<BusinessCategory>>().Object,
            adjustments.Object,
            new Mock<IPaymentAccountService>().Object);

        var result = await service.ToggleStockTrackingAsync(
            ownerId,
            business.Id,
            new ToggleStockTrackingRequest { IsStockTrackingEnabled = false });

        Assert.False(result.IsStockTrackingEnabled);
        adjustments.Verify(x => x.ReconcileAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<ReconcileInventoryRequest>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProductUpdate_WithHistoryRejectsUnitAndCacheChanges()
    {
        var ownerId = Guid.NewGuid();
        var business = Business(ownerId);
        var product = Product(business.Id, stock: 2m);
        product.Unit = "cái";
        product.CostPrice = 10m;
        var products = new Mock<IProductRepository>();
        products.Setup(x => x.GetByIdWithPricesAsync(product.Id))
            .ReturnsAsync(product);
        var businesses = new Mock<IGenericRepository<BusinessProfile>>();
        businesses.Setup(x => x.GetByIdAsync(business.Id)).ReturnsAsync(business);
        _controls.Setup(x => x.HasMovementsForProductAsync(
                product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = new ProductService(
            _unitOfWork.Object,
            products.Object,
            businesses.Object,
            new Mock<IGenericRepository<BusinessCategory>>().Object,
            _controls.Object);

        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(
            ownerId,
            product.Id,
            new UpdateProductRequest
            {
                ProductCode = product.ProductCode,
                Name = product.Name,
                Unit = "hộp",
                CostPrice = 20m,
                StockQuantity = 3m
            }));

        _unitOfWork.Verify(x => x.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngredientUpdate_WithHistoryRejectsUnitAndCacheChanges()
    {
        var ownerId = Guid.NewGuid();
        var business = Business(ownerId);
        var ingredient = Ingredient(business.Id, stock: 2m);
        ingredient.Unit = "kg";
        ingredient.EstimatedPrice = 10m;
        var ingredients = new Mock<IIngredientRepository>();
        ingredients.Setup(x => x.GetByIdAsync(ingredient.Id))
            .ReturnsAsync(ingredient);
        var businesses = new Mock<IGenericRepository<BusinessProfile>>();
        businesses.Setup(x => x.GetByIdAsync(business.Id)).ReturnsAsync(business);
        _controls.Setup(x => x.HasMovementsForIngredientAsync(
                ingredient.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = new IngredientService(
            _unitOfWork.Object,
            ingredients.Object,
            businesses.Object,
            _controls.Object);

        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(
            ownerId,
            ingredient.Id,
            new UpdateIngredientRequest
            {
                Name = ingredient.Name,
                Unit = "g",
                EstimatedPrice = 20m,
                StockQuantity = 3m
            }));

        _unitOfWork.Verify(x => x.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private (Guid OwnerId, BusinessProfile Business) SetupOwnedBusiness(bool enabled = true)
    {
        var ownerId = Guid.NewGuid();
        var business = Business(ownerId, enabled);
        _controls.Setup(x => x.GetBusinessAsync(
                business.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(business);
        return (ownerId, business);
    }

    private void SetupActiveItems(
        Guid businessId,
        IReadOnlyList<Product> products,
        IReadOnlyList<Ingredient> ingredients)
    {
        _controls.Setup(x => x.GetActiveProductsAsync(
                businessId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);
        _controls.Setup(x => x.GetActiveIngredientsAsync(
                businessId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ingredients);
    }

    private InventoryInitializationService CreateInitializationService() => new(
        _unitOfWork.Object,
        _controls.Object,
        _movements.Object,
        _guard.Object);

    private InventoryAdjustmentService CreateAdjustmentService() => new(
        _unitOfWork.Object,
        _controls.Object,
        _movements.Object,
        _guard.Object);

    private static BusinessProfile Business(Guid ownerId, bool enabled = true) => new()
    {
        Id = Guid.NewGuid(),
        OwnerId = ownerId,
        BusinessName = "Test business",
        IsActive = true,
        IsStockTrackingEnabled = enabled
    };

    private static Product Product(Guid businessId, decimal stock) => new()
    {
        Id = Guid.NewGuid(),
        BusinessId = businessId,
        ProductCode = "SP-01",
        Name = "Product",
        Status = ProductStatus.Active,
        StockQuantity = stock
    };

    private static Ingredient Ingredient(Guid businessId, decimal stock) => new()
    {
        Id = Guid.NewGuid(),
        BusinessId = businessId,
        Name = "Ingredient",
        StockQuantity = stock
    };

    private static InventoryMovement Movement(
        Guid businessId,
        Guid productId,
        decimal quantity,
        string type = InventoryMovementTypes.OpeningBalance) => new()
    {
        InventoryMovementId = Guid.NewGuid(),
        BusinessId = businessId,
        ProductId = productId,
        MovementType = type,
        Quantity = quantity,
        TotalValue = type == InventoryMovementTypes.OrderOut ? null : 1m
    };

    private static InventoryMovement IngredientMovement(
        Guid businessId,
        Guid ingredientId,
        decimal quantity) => new()
    {
        InventoryMovementId = Guid.NewGuid(),
        BusinessId = businessId,
        IngredientId = ingredientId,
        MovementType = InventoryMovementTypes.OpeningBalance,
        Quantity = quantity,
        TotalValue = 1m
    };

    private static InitializeInventoryRequest OpeningRequest() => new()
    {
        OccurredAt = new DateTime(2026, 1, 1),
        DocumentNumber = "OPEN-01",
        Description = "Opening"
    };

    [Fact]
    public async Task Reconcile_StaleVersionDoesNotWriteOrEnableTracking()
    {
        var (ownerId, business) = SetupOwnedBusiness(enabled: false);
        var product = Product(business.Id, stock: 10m);
        SetupActiveItems(business.Id, [product], []);
        _controls.Setup(x => x.GetMovementsAsync(business.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Movement(business.Id, product.Id, 10m)]);
        await Assert.ThrowsAsync<ConflictException>(() => CreateAdjustmentService().ReconcileAsync(
            ownerId, business.Id, Reconcile(product.Id, 9m, null, "stale"), true));
        Assert.False(business.IsStockTrackingEnabled);
        Assert.Equal(10m, product.StockQuantity);
        _movements.Verify(x => x.StageAdjustmentAsync(It.IsAny<StageInventoryAdjustmentCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private async Task<string> CurrentVersion(Guid businessId) => InventoryControlRules.Version(
        await _controls.Object.GetActiveProductsAsync(businessId, true),
        await _controls.Object.GetActiveIngredientsAsync(businessId, true),
        await _controls.Object.GetMovementsAsync(businessId, true));

    private static ReconcileInventoryRequest Reconcile(
        Guid productId,
        decimal actual,
        decimal? value, string version) => new()
    {
        ExpectedVersion = version,
        OccurredAt = new DateTime(2026, 8, 20),
        DocumentNumber = "KK-01",
        Description = "Stocktake",
        Lines =
        [
            new InventoryCountLineRequest
            {
                ProductId = productId,
                ActualQuantity = actual,
                AdjustmentInTotalValue = value
            }
        ]
    };
}
