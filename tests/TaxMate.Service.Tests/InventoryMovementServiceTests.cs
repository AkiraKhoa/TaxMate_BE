using Moq;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class InventoryMovementServiceTests
{
    private readonly Mock<IInventoryMovementRepository> _repository = new();
    private readonly Mock<IInventoryMovementCoordinatorValidator> _coordinatorValidator = new();

    [Fact]
    public async Task StageReplaceSource_AggregatesDuplicateItemLines()
    {
        var businessId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        SetupProducts(new Product
        {
            Id = productId,
            BusinessId = businessId,
            ProductCode = "SP01",
            Name = "Product"
        });
        SetupNoExistingSource();
        var added = new List<InventoryMovement>();
        _repository.Setup(x => x.AddAsync(It.IsAny<InventoryMovement>()))
            .Callback<InventoryMovement>(added.Add)
            .Returns(Task.CompletedTask);

        var result = await CreateService().StageReplaceSourceAsync(new()
        {
            BusinessId = businessId,
            MovementType = InventoryMovementTypes.PurchaseIn,
            ReferenceId = Guid.NewGuid(),
            OccurredAt = new DateTime(2026, 1, 2),
            DocumentNumber = " PNK-01 ",
            Description = " Nhập hàng ",
            Lines =
            [
                new()
                {
                    ProductId = productId,
                    Quantity = 2m,
                    TotalValue = 200_000m
                },
                new()
                {
                    ProductId = productId,
                    Quantity = 3m,
                    TotalValue = 450_000m
                }
            ]
        });

        var movement = Assert.Single(result);
        Assert.Same(movement, Assert.Single(added));
        Assert.Equal(5m, movement.Quantity);
        Assert.Equal(650_000m, movement.TotalValue);
        Assert.Equal("PNK-01", movement.DocumentNumber);
        Assert.Equal("Nhập hàng", movement.Description);
        Assert.Equal(DateTimeKind.Unspecified, movement.OccurredAt.Kind);
        _coordinatorValidator.Verify(x => x.EnsureValidReferenceTargetAsync(
            It.Is<InventoryMovementReferenceTarget>(target =>
                target.BusinessId == businessId &&
                target.MovementType == InventoryMovementTypes.PurchaseIn),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StageReplaceSource_UpdatesAndRemovesExistingLines()
    {
        var businessId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var removedIngredientId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        SetupItems(
            [
                new Product
                {
                    Id = productId,
                    BusinessId = businessId,
                    ProductCode = "SP01",
                    Name = "Product"
                }
            ],
            [
                new Ingredient
                {
                    Id = removedIngredientId,
                    BusinessId = businessId,
                    Name = "Old ingredient"
                }
            ]);
        var kept = Movement(
            businessId,
            productId,
            null,
            InventoryMovementTypes.PurchaseIn,
            1m,
            10m,
            referenceId);
        var removed = Movement(
            businessId,
            null,
            removedIngredientId,
            InventoryMovementTypes.PurchaseIn,
            1m,
            20m,
            referenceId);
        _repository.Setup(x => x.GetBySourceForUpdateAsync(
                businessId,
                InventoryMovementTypes.PurchaseIn,
                referenceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([kept, removed]);

        await CreateService().StageReplaceSourceAsync(new()
        {
            BusinessId = businessId,
            MovementType = InventoryMovementTypes.PurchaseIn,
            ReferenceId = referenceId,
            OccurredAt = Utc(2026, 2, 1),
            DocumentNumber = "PNK-02",
            Description = "Updated",
            Lines =
            [
                new()
                {
                    ProductId = productId,
                    Quantity = 4m,
                    TotalValue = 80m
                }
            ]
        });

        Assert.Equal(4m, kept.Quantity);
        Assert.Equal(80m, kept.TotalValue);
        _repository.Verify(x => x.Update(kept), Times.Once);
        _repository.Verify(
            x => x.RemoveRange(It.Is<IEnumerable<InventoryMovement>>(
                values => values.Single() == removed)),
            Times.Once);
    }

    [Fact]
    public async Task StageReplaceSource_RejectsCrossBusinessItem()
    {
        var businessId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        SetupProducts(new Product
        {
            Id = productId,
            BusinessId = Guid.NewGuid(),
            ProductCode = "SP01",
            Name = "Other business product"
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            CreateService().StageReplaceSourceAsync(new()
            {
                BusinessId = businessId,
                MovementType = InventoryMovementTypes.PurchaseIn,
                ReferenceId = Guid.NewGuid(),
                OccurredAt = Utc(2026, 1, 1),
                DocumentNumber = "PNK",
                Description = "Purchase",
                Lines =
                [
                    new()
                    {
                        ProductId = productId,
                        Quantity = 1m,
                        TotalValue = 10m
                    }
                ]
            }));

        Assert.Contains("không thuộc", exception.Message);
        _repository.Verify(
            x => x.AddAsync(It.IsAny<InventoryMovement>()),
            Times.Never);
    }

    [Fact]
    public async Task StageOrderOut_RejectsCallerSuppliedValue()
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            CreateService().StageReplaceSourceAsync(new()
            {
                BusinessId = Guid.NewGuid(),
                MovementType = InventoryMovementTypes.OrderOut,
                ReferenceId = Guid.NewGuid(),
                OccurredAt = Utc(2026, 1, 1),
                DocumentNumber = "DH-01",
                Description = "Order",
                Lines =
                [
                    new()
                    {
                        ProductId = Guid.NewGuid(),
                        Quantity = 1m,
                        TotalValue = 10m
                    }
                ]
            }));

        Assert.Contains("hệ thống tính", exception.Message);
    }

    [Fact]
    public async Task StageAdjustmentIn_AllowsMissingValueButKeepsItNull()
    {
        var businessId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        SetupIngredients(new Ingredient
        {
            Id = ingredientId,
            BusinessId = businessId,
            Name = "Ingredient"
        });
        _repository.Setup(x => x.AddAsync(It.IsAny<InventoryMovement>()))
            .Returns(Task.CompletedTask);

        var result = await CreateService().StageAdjustmentAsync(new()
        {
            BusinessId = businessId,
            MovementType = InventoryMovementTypes.AdjustmentIn,
            IngredientId = ingredientId,
            Quantity = 2m,
            TotalValue = null,
            OccurredAt = Utc(2026, 3, 31),
            DocumentNumber = "KK-01",
            Description = "Kiểm kho"
        });

        Assert.Null(result.TotalValue);
        Assert.Null(result.ReferenceId);
        Assert.Equal(ingredientId, result.IngredientId);
    }

    [Fact]
    public async Task StageMovement_RejectsHostLocalOccurredAt()
    {
        var businessId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        SetupProducts(new Product
        {
            Id = productId,
            BusinessId = businessId,
            ProductCode = "SP01",
            Name = "Product"
        });
        SetupNoExistingSource();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().StageReplaceSourceAsync(new()
            {
                BusinessId = businessId,
                MovementType = InventoryMovementTypes.PurchaseIn,
                ReferenceId = Guid.NewGuid(),
                OccurredAt = new DateTime(
                    2026,
                    1,
                    2,
                    0,
                    0,
                    0,
                    DateTimeKind.Local),
                DocumentNumber = "PNK",
                Description = "Purchase",
                Lines =
                [
                    new()
                    {
                        ProductId = productId,
                        Quantity = 1m,
                        TotalValue = 10m
                    }
                ]
            }));
    }

    private InventoryMovementService CreateService() => new(
        _repository.Object,
        _coordinatorValidator.Object);

    private void SetupNoExistingSource()
    {
        _repository.Setup(x => x.GetBySourceForUpdateAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private void SetupProducts(params Product[] products)
    {
        SetupItems(products, []);
    }

    private void SetupIngredients(params Ingredient[] ingredients)
    {
        SetupItems([], ingredients);
    }

    private void SetupItems(
        IReadOnlyCollection<Product> products,
        IReadOnlyCollection<Ingredient> ingredients)
    {
        _repository.Setup(x => x.GetProductsIncludingDeletedAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                products.Where(x => ids.Contains(x.Id)).ToList());
        _repository.Setup(x => x.GetIngredientsIncludingDeletedAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                ingredients.Where(x => ids.Contains(x.Id)).ToList());
    }

    private static InventoryMovement Movement(
        Guid businessId,
        Guid? productId,
        Guid? ingredientId,
        string movementType,
        decimal quantity,
        decimal? totalValue,
        Guid? referenceId) => new()
    {
        InventoryMovementId = Guid.NewGuid(),
        BusinessId = businessId,
        ProductId = productId,
        IngredientId = ingredientId,
        MovementType = movementType,
        Quantity = quantity,
        TotalValue = totalValue,
        OccurredAt = Utc(2026, 1, 1),
        DocumentNumber = "DOC",
        Description = "Description",
        ReferenceId = referenceId
    };

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
