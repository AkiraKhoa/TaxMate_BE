using Moq;
using TaxMate.Model.Common;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class ProductServiceAvailableQuantityTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IGenericRepository<BusinessProfile>> _businessProfiles = new();
    private readonly Mock<IGenericRepository<BusinessCategory>> _businessCategories = new();
    private readonly Mock<IInventoryControlRepository> _inventoryControls = new();

    [Fact]
    public async Task GetPaged_WithRecipe_ReturnsLimitingWholeProductQuantity()
    {
        var product = CreateProduct(stockQuantity: null);
        product.ProductIngredients =
        [
            CreateRecipeItem(product.Id, quantity: 2.5m, ingredientStock: 26m),
            CreateRecipeItem(product.Id, quantity: 1m, ingredientStock: 7.9m)
        ];

        var result = await GetPagedAsync(product);

        var response = Assert.Single(result.Items);
        Assert.True(response.HasRecipe);
        Assert.Equal(7m, response.AvailableQuantity);
    }

    [Fact]
    public async Task GetPaged_WithRecipeAndNegativeStock_ClampsAvailableQuantityToZero()
    {
        var product = CreateProduct(stockQuantity: null);
        product.ProductIngredients =
        [
            CreateRecipeItem(product.Id, quantity: 1m, ingredientStock: -2m)
        ];

        var result = await GetPagedAsync(product);

        Assert.Equal(0m, Assert.Single(result.Items).AvailableQuantity);
    }

    [Fact]
    public async Task GetPaged_WithoutRecipe_ReturnsDirectProductStock()
    {
        var product = CreateProduct(stockQuantity: 4.5m);

        var result = await GetPagedAsync(product);

        var response = Assert.Single(result.Items);
        Assert.False(response.HasRecipe);
        Assert.Equal(4.5m, response.AvailableQuantity);
    }

    [Fact]
    public async Task GetPaged_WithInvalidRecipeQuantity_ReturnsUnknownAvailability()
    {
        var product = CreateProduct(stockQuantity: null);
        product.ProductIngredients =
        [
            CreateRecipeItem(product.Id, quantity: 0m, ingredientStock: 10m)
        ];

        var result = await GetPagedAsync(product);

        Assert.Null(Assert.Single(result.Items).AvailableQuantity);
    }

    private async Task<PagedResult<TaxMate.Model.DTO.ProductResponse>> GetPagedAsync(Product product)
    {
        var ownerId = Guid.NewGuid();
        _businessProfiles
            .Setup(x => x.GetByIdAsync(product.BusinessId))
            .ReturnsAsync(new BusinessProfile
            {
                Id = product.BusinessId,
                OwnerId = ownerId,
                BusinessName = "Test business"
            });
        _products
            .Setup(x => x.GetPagedByBusinessAsync(
                product.BusinessId,
                1,
                10,
                null,
                ProductStatus.Active,
                null,
                null))
            .ReturnsAsync(([product], 1));

        return await CreateService().GetPagedByBusinessAsync(
            ownerId,
            product.BusinessId,
            1,
            10,
            null,
            ProductStatus.Active,
            null,
            null);
    }

    private ProductService CreateService() => new(
        _unitOfWork.Object,
        _products.Object,
        _businessProfiles.Object,
        _businessCategories.Object,
        _inventoryControls.Object);

    private static Product CreateProduct(decimal? stockQuantity) => new()
    {
        Id = Guid.NewGuid(),
        BusinessId = Guid.NewGuid(),
        ProductCode = "SP001",
        Name = "Test product",
        Status = ProductStatus.Active,
        StockQuantity = stockQuantity
    };

    private static ProductIngredient CreateRecipeItem(
        Guid productId,
        decimal quantity,
        decimal ingredientStock) => new()
    {
        ProductId = productId,
        IngredientId = Guid.NewGuid(),
        Quantity = quantity,
        Ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            BusinessId = Guid.NewGuid(),
            Name = "Ingredient",
            StockQuantity = ingredientStock
        }
    };
}
