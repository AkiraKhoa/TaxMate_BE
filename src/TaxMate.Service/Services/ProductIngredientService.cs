using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class ProductIngredientService : IProductIngredientService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductIngredientRepository _productIngredients;
    private readonly IProductRepository _products;
    private readonly IIngredientRepository _ingredients;
    private readonly IGenericRepository<BusinessProfile> _businessProfiles;

    public ProductIngredientService(
        IUnitOfWork unitOfWork,
        IProductIngredientRepository productIngredients,
        IProductRepository products,
        IIngredientRepository ingredients,
        IGenericRepository<BusinessProfile> businessProfiles)
    {
        _unitOfWork = unitOfWork;
        _productIngredients = productIngredients;
        _products = products;
        _ingredients = ingredients;
        _businessProfiles = businessProfiles;
    }

    public async Task<ProductIngredientResponse> AddAsync(
        Guid ownerId,
        Guid productId,
        AddProductIngredientRequest request)
    {
        await EnsureProductOwnerAsync(ownerId, productId);
        await EnsureIngredientAvailableAsync(request.IngredientId);

        if (request.Quantity <= 0)
            throw new BadRequestException("Quantity must be greater than zero.");

        if (await _productIngredients.ExistsAsync(productId, request.IngredientId))
            throw new ConflictException("This ingredient is already linked to the product.");

        var entity = new ProductIngredient
        {
            ProductId = productId,
            IngredientId = request.IngredientId,
            Quantity = request.Quantity
        };

        await _productIngredients.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var created = await _productIngredients.GetByCompositeKeyAsync(productId, request.IngredientId);
        return MapToResponse(created!);
    }

    public async Task<ProductIngredientResponse> UpdateAsync(
        Guid ownerId,
        Guid productId,
        Guid ingredientId,
        UpdateProductIngredientRequest request)
    {
        await EnsureProductOwnerAsync(ownerId, productId);

        if (request.Quantity <= 0)
            throw new BadRequestException("Quantity must be greater than zero.");

        var entity = await _productIngredients.GetByCompositeKeyAsync(productId, ingredientId);
        if (entity is null)
            throw new NotFoundException(
                $"Product ingredient link for product '{productId}' and ingredient '{ingredientId}' not found.");

        entity.Quantity = request.Quantity;

        _productIngredients.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponse(entity);
    }

    public async Task DeleteAsync(Guid ownerId, Guid productId, Guid ingredientId)
    {
        await EnsureProductOwnerAsync(ownerId, productId);

        var entity = await _productIngredients.GetByCompositeKeyAsync(productId, ingredientId);
        if (entity is null)
            throw new NotFoundException(
                $"Product ingredient link for product '{productId}' and ingredient '{ingredientId}' not found.");

        _productIngredients.Remove(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProductIngredientResponse>> GetByProductIdAsync(
        Guid ownerId,
        Guid productId)
    {
        await EnsureProductOwnerAsync(ownerId, productId);

        var items = await _productIngredients.GetByProductIdAsync(productId);
        return items.Select(MapToResponse);
    }

    private async Task EnsureProductOwnerAsync(Guid ownerId, Guid productId)
    {
        var product = await _products.GetByIdAsync(productId);
        if (product is null)
            throw new NotFoundException($"Product with id '{productId}' not found.");

        var business = await _businessProfiles.GetByIdAsync(product.BusinessId);
        if (business is null)
            throw new NotFoundException("Business profile not found.");

        if (business.OwnerId != ownerId)
            throw new UnauthorizedAccessException("You do not own this business.");
    }

    private async Task EnsureIngredientAvailableAsync(Guid ingredientId)
    {
        var ingredient = await _ingredients.GetByIdAsync(ingredientId);
        if (ingredient is null || ingredient.IsDeleted)
            throw new NotFoundException($"Ingredient with id '{ingredientId}' not found.");
    }

    private static ProductIngredientResponse MapToResponse(ProductIngredient entity)
    {
        return new ProductIngredientResponse
        {
            ProductId = entity.ProductId,
            IngredientId = entity.IngredientId,
            IngredientName = entity.Ingredient.Name,
            Unit = entity.Ingredient.Unit,
            Quantity = entity.Quantity
        };
    }
}
