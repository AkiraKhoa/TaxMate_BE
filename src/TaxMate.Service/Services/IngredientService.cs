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

    public IngredientService(
        IUnitOfWork unitOfWork,
        IIngredientRepository ingredients)
    {
        _unitOfWork = unitOfWork;
        _ingredients = ingredients;
    }

    public async Task<IngredientResponse> CreateAsync(CreateIngredientRequest request)
    {
        // Check duplicate name
        var exists = await _ingredients
            .AnyAsync(x => x.Name.ToLower() == request.Name.ToLower() && !x.IsDeleted);

        if (exists)
            throw new ConflictException($"Ingredient with name '{request.Name}' already exists.");

        var entity = new Ingredient
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Unit = request.Unit,
            EstimatedPrice = request.EstimatedPrice,
            IsDeleted = false
        };

        await _ingredients.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponse(entity);
    }

    public async Task<IngredientResponse> UpdateAsync(Guid id, UpdateIngredientRequest request)
    {
        var entity = await _ingredients.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException($"Ingredient with id '{id}' not found.");

        if (entity.IsDeleted)
            throw new ConflictException($"Ingredient with id '{id}' has been deactivated.");

        // Check duplicate name (exclude current)
        var duplicate = await _ingredients
            .AnyAsync(x => x.Name.ToLower() == request.Name.ToLower()
                           && x.Id != id
                           && !x.IsDeleted);

        if (duplicate)
            throw new ConflictException($"Ingredient with name '{request.Name}' already exists.");

        entity.Name = request.Name;
        entity.Unit = request.Unit;
        entity.EstimatedPrice = request.EstimatedPrice;

        _ingredients.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponse(entity);
    }

    public async Task DeactivateAsync(Guid id)
    {
        var entity = await _ingredients.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException($"Ingredient with id '{id}' not found.");

        if (entity.IsDeleted)
            throw new ConflictException($"Ingredient with id '{id}' is already deactivated.");

        entity.IsDeleted = true;
        _ingredients.Update(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<PagedResult<IngredientResponse>> GetPagedAsync(
        int pageNumber, int pageSize, string? search)
    {
        var (items, totalCount) = await _ingredients
            .GetPagedAsync(pageNumber, pageSize, search);

        return new PagedResult<IngredientResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IngredientResponse> GetByIdAsync(Guid id)
    {
        var entity = await _ingredients.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException($"Ingredient with id '{id}' not found.");

        return MapToResponse(entity);
    }

    private static IngredientResponse MapToResponse(Ingredient entity)
    {
        return new IngredientResponse
        {
            Id = entity.Id,
            Name = entity.Name,
            Unit = entity.Unit,
            EstimatedPrice = entity.EstimatedPrice,
            IsDeleted = entity.IsDeleted,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
