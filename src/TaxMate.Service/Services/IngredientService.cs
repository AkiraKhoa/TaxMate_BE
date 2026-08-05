using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class IngredientService : IIngredientService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIngredientRepository _ingredients;
    private readonly IGenericRepository<BusinessProfile> _businessProfiles;

    public IngredientService(
        IUnitOfWork unitOfWork,
        IIngredientRepository ingredients,
        IGenericRepository<BusinessProfile> businessProfiles)
    {
        _unitOfWork = unitOfWork;
        _ingredients = ingredients;
        _businessProfiles = businessProfiles;
    }

    public async Task<IngredientResponse> CreateAsync(
        Guid ownerId,
        Guid businessId,
        CreateIngredientRequest request)
    {
        await EnsureBusinessOwnerAsync(businessId, ownerId);

        var exists = await _ingredients.AnyAsync(x =>
            x.BusinessId == businessId
            && x.Name.ToLower() == request.Name.ToLower()
            && !x.IsDeleted);

        if (exists)
            throw new ConflictException($"Ingredient with name '{request.Name}' already exists.");

        var entity = new Ingredient
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Name = request.Name.Trim(),
            Unit = request.Unit,
            EstimatedPrice = request.EstimatedPrice,
            IsDeleted = false
        };

        await _ingredients.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponse(entity);
    }

    public async Task<IngredientResponse> UpdateAsync(
        Guid ownerId,
        Guid id,
        UpdateIngredientRequest request)
    {
        var entity = await _ingredients.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException($"Ingredient with id '{id}' not found.");

        await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);

        if (entity.IsDeleted)
            throw new ConflictException($"Ingredient with id '{id}' has been deactivated.");

        var duplicate = await _ingredients.AnyAsync(x =>
            x.BusinessId == entity.BusinessId
            && x.Name.ToLower() == request.Name.ToLower()
            && x.Id != id
            && !x.IsDeleted);

        if (duplicate)
            throw new ConflictException($"Ingredient with name '{request.Name}' already exists.");

        entity.Name = request.Name.Trim();
        entity.Unit = request.Unit;
        entity.EstimatedPrice = request.EstimatedPrice;

        _ingredients.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponse(entity);
    }

    public async Task DeactivateAsync(Guid ownerId, Guid id)
    {
        var entity = await _ingredients.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException($"Ingredient with id '{id}' not found.");

        await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);

        if (entity.IsDeleted)
            throw new ConflictException($"Ingredient with id '{id}' is already deactivated.");

        entity.IsDeleted = true;
        _ingredients.Update(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<PagedResult<IngredientResponse>> GetPagedByBusinessAsync(
        Guid ownerId,
        Guid businessId,
        int pageNumber,
        int pageSize,
        string? search)
    {
        await EnsureBusinessOwnerAsync(businessId, ownerId);

        var (items, totalCount) = await _ingredients
            .GetPagedByBusinessAsync(businessId, pageNumber, pageSize, search);

        return new PagedResult<IngredientResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IngredientResponse> GetByIdAsync(Guid ownerId, Guid id)
    {
        var entity = await _ingredients.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException($"Ingredient with id '{id}' not found.");

        await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);

        return MapToResponse(entity);
    }

    private async Task EnsureBusinessOwnerAsync(Guid businessId, Guid ownerId)
    {
        var business = await _businessProfiles.GetByIdAsync(businessId);
        if (business is null)
            throw new NotFoundException("Business profile not found.");

        if (business.OwnerId != ownerId)
            throw new UnauthorizedAccessException("You do not own this business.");
    }

    private static IngredientResponse MapToResponse(Ingredient entity)
    {
        return new IngredientResponse
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            Name = entity.Name,
            Unit = entity.Unit,
            EstimatedPrice = entity.EstimatedPrice,
            StockQuantity = entity.StockQuantity,
            IsDeleted = entity.IsDeleted,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
