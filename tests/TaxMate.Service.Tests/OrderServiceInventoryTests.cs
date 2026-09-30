using System.Reflection;
using Moq;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class OrderServiceInventoryTests
{
    [Fact]
    public async Task ServiceOrder_DoesNotCreateStockOutOrDecrementStock()
    {
        var products = new Mock<IProductRepository>();
        var productIngredients = new Mock<IProductIngredientRepository>();
        var ingredients = new Mock<IIngredientRepository>();
        var movements = new Mock<IInventoryMovementService>();
        var productId = Guid.NewGuid();
        productIngredients.Setup(x => x.GetByProductIdAsync(productId)).ReturnsAsync([]);
        var service = CreateService(products, productIngredients, ingredients, movements);

        await DeductInventoryAsync(service, CreateOrder(productId), BusinessCategoryIds.ServiceStore);

        productIngredients.Verify(x => x.GetByProductIdAsync(It.IsAny<Guid>()), Times.Never);
        movements.Verify(x => x.StageReplaceSourceAsync(
            It.IsAny<ReplaceInventorySourceMovementsCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        products.Verify(x => x.DecrementStockAsync(It.IsAny<Guid>(), It.IsAny<decimal>()), Times.Never);
    }

    [Fact]
    public async Task GoodsOrder_StillCreatesStockOutAndDecrementsStock()
    {
        var products = new Mock<IProductRepository>();
        var productIngredients = new Mock<IProductIngredientRepository>();
        var ingredients = new Mock<IIngredientRepository>();
        var movements = new Mock<IInventoryMovementService>();
        var productId = Guid.NewGuid();
        productIngredients.Setup(x => x.GetByProductIdAsync(productId)).ReturnsAsync([]);
        var service = CreateService(products, productIngredients, ingredients, movements);

        await DeductInventoryAsync(service, CreateOrder(productId), BusinessCategoryIds.DistGoods);

        movements.Verify(x => x.StageReplaceSourceAsync(
            It.Is<ReplaceInventorySourceMovementsCommand>(command =>
                command.MovementType == InventoryMovementTypes.OrderOut &&
                command.Lines.Any(line => line.ProductId == productId && line.Quantity == 1)),
            It.IsAny<CancellationToken>()), Times.Once);
        products.Verify(x => x.DecrementStockAsync(productId, 1), Times.Once);
    }

    private static OrderService CreateService(
        Mock<IProductRepository> products,
        Mock<IProductIngredientRepository> productIngredients,
        Mock<IIngredientRepository> ingredients,
        Mock<IInventoryMovementService> movements) =>
        new(
            null!, null!, null!, null!, products.Object,
            null!, null!, null!, null!, null!, null!, null!,
            productIngredients.Object, ingredients.Object,
            null!, null!, null!, movements.Object, null!, null!);

    private static Transaction CreateOrder(Guid productId) => new()
    {
        BusinessId = Guid.NewGuid(),
        TransactionId = Guid.NewGuid(),
        TransactionCode = "TEST-ORDER",
        CompletedAt = DateTime.UtcNow,
        TransactionItems = [new TransactionItem { ProductId = productId, Quantity = 1 }]
    };

    private static async Task DeductInventoryAsync(OrderService service, Transaction order, Guid? mainCategoryId)
    {
        var method = typeof(OrderService).GetMethod(
            "DeductInventoryForCompletedOrderAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var arguments = method.GetParameters().Length == 1 ? new object?[] { order } : [order, mainCategoryId];
        await (Task)method.Invoke(service, arguments)!;
    }
}
