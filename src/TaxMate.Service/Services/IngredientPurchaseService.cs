using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class IngredientPurchaseService : IIngredientPurchaseService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIngredientPurchaseRepository _purchases;
    private readonly IGenericRepository<BusinessProfile> _businesses;
    private readonly IIngredientRepository _ingredients;

    public IngredientPurchaseService(
        IUnitOfWork unitOfWork,
        IIngredientPurchaseRepository purchases,
        IGenericRepository<BusinessProfile> businesses,
        IIngredientRepository ingredients)
    {
        _unitOfWork = unitOfWork;
        _purchases = purchases;
        _businesses = businesses;
        _ingredients = ingredients;
    }

    public async Task<IngredientPurchaseResponse> CreateAsync(Guid businessId, CreateIngredientPurchaseRequest request)
    {
        var businessExists = await _businesses.AnyAsync(x => x.Id == businessId);
        if (!businessExists)
            throw new NotFoundException($"Business profile with id '{businessId}' not found.");

        var ingredient = await _ingredients.GetByIdAsync(request.IngredientId);
        if (ingredient is null || ingredient.IsDeleted)
            throw new NotFoundException($"Ingredient with id '{request.IngredientId}' not found or deactivated.");

        var entity = new IngredientPurchase
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            IngredientId = request.IngredientId,
            Quantity = request.Quantity,
            TotalCost = request.TotalCost,
            PurchaseDate = request.PurchaseDate.ToUniversalTime(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _purchases.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var details = await _purchases.GetByIdWithDetailsAsync(entity.Id);
        if (details is null)
            throw new NotFoundException($"Ingredient purchase with id '{entity.Id}' could not be re-loaded.");

        return MapToResponse(details);
    }

    public async Task<IngredientPurchaseResponse> UpdateAsync(Guid id, UpdateIngredientPurchaseRequest request)
    {
        var entity = await _purchases.GetByIdWithDetailsAsync(id);
        if (entity is null)
            throw new NotFoundException($"Ingredient purchase with id '{id}' not found.");

        if (entity.IngredientId != request.IngredientId)
        {
            var ingredient = await _ingredients.GetByIdAsync(request.IngredientId);
            if (ingredient is null || ingredient.IsDeleted)
                throw new NotFoundException($"Ingredient with id '{request.IngredientId}' not found or deactivated.");
            entity.IngredientId = request.IngredientId;
        }

        entity.Quantity = request.Quantity;
        entity.TotalCost = request.TotalCost;
        entity.PurchaseDate = request.PurchaseDate.ToUniversalTime();
        entity.UpdatedAt = DateTime.UtcNow;

        _purchases.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        var details = await _purchases.GetByIdWithDetailsAsync(id);
        if (details is null)
            throw new NotFoundException($"Ingredient purchase with id '{id}' could not be re-loaded.");

        return MapToResponse(details);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _purchases.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException($"Ingredient purchase with id '{id}' not found.");

        _purchases.Remove(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IngredientPurchaseResponse> GetByIdAsync(Guid id)
    {
        var entity = await _purchases.GetByIdWithDetailsAsync(id);
        if (entity is null)
            throw new NotFoundException($"Ingredient purchase with id '{id}' not found.");

        return MapToResponse(entity);
    }

    public async Task<PagedResult<IngredientPurchaseResponse>> GetPagedByBusinessAsync(
        Guid businessId, int pageNumber, int pageSize, string? search)
    {
        var businessExists = await _businesses.AnyAsync(x => x.Id == businessId);
        if (!businessExists)
            throw new NotFoundException($"Business profile with id '{businessId}' not found.");

        var (items, totalCount) = await _purchases.GetPagedByBusinessAsync(
            businessId, pageNumber, pageSize, search);

        return new PagedResult<IngredientPurchaseResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    private static IngredientPurchaseResponse MapToResponse(IngredientPurchase entity)
    {
        return new IngredientPurchaseResponse
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            BusinessName = entity.Business?.BusinessName ?? string.Empty,
            IngredientId = entity.IngredientId,
            IngredientName = entity.Ingredient?.Name ?? string.Empty,
            IngredientUnit = entity.Ingredient?.Unit,
            Quantity = entity.Quantity,
            TotalCost = entity.TotalCost,
            PurchaseDate = entity.PurchaseDate,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
