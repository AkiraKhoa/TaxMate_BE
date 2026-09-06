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
    private readonly IInventoryControlRepository _inventoryControls;

    public IngredientService(
        IUnitOfWork unitOfWork,
        IIngredientRepository ingredients,
        IGenericRepository<BusinessProfile> businessProfiles,
        IInventoryControlRepository inventoryControls)
    {
        _unitOfWork = unitOfWork;
        _ingredients = ingredients;
        _businessProfiles = businessProfiles;
        _inventoryControls = inventoryControls;
    }

    public async Task<IngredientResponse> CreateAsync(
        Guid ownerId,
        Guid businessId,
        CreateIngredientRequest request)
    {
        await EnsureBusinessOwnerAsync(businessId, ownerId);
        EnsureNonNegative(request.StockQuantity, "Số lượng tồn");
        EnsureNonNegative(request.EstimatedPrice, "Giá ước tính");

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
            StockQuantity = request.StockQuantity,
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

        var hasMovement = await _inventoryControls.HasMovementsForIngredientAsync(id);
        if (hasMovement)
        {
            if (!string.IsNullOrWhiteSpace(entity.Unit))
                EnsureHistoryProtectedFieldUnchanged(
                NormalizeUnit(entity.Unit),
                NormalizeUnit(request.Unit),
                "đơn vị tính");
            if (request.EstimatedPrice.HasValue)
            {
                EnsureHistoryProtectedFieldUnchanged(
                    entity.EstimatedPrice,
                    request.EstimatedPrice,
                    "giá ước tính");
            }
            if (request.StockQuantity.HasValue)
            {
                EnsureHistoryProtectedFieldUnchanged(
                    entity.StockQuantity,
                    request.StockQuantity.Value,
                    "số lượng tồn");
            }
        }
        else
        {
            EnsureNonNegative(request.StockQuantity, "Số lượng tồn");
            EnsureNonNegative(request.EstimatedPrice, "Giá ước tính");
        }

        var duplicate = await _ingredients.AnyAsync(x =>
            x.BusinessId == entity.BusinessId
            && x.Name.ToLower() == request.Name.ToLower()
            && x.Id != id
            && !x.IsDeleted);

        if (duplicate)
            throw new ConflictException($"Ingredient with name '{request.Name}' already exists.");

        entity.Name = request.Name.Trim();
        if (hasMovement && string.IsNullOrWhiteSpace(entity.Unit))
            entity.Unit = NormalizeUnit(request.Unit);
        if (!hasMovement)
        {
            entity.Unit = request.Unit;
            if (request.EstimatedPrice.HasValue)
                entity.EstimatedPrice = request.EstimatedPrice;
            if (request.StockQuantity.HasValue)
                entity.StockQuantity = request.StockQuantity.Value;
        }

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

    private static string? NormalizeUnit(string? unit) =>
        string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();

    private static void EnsureNonNegative(decimal? value, string fieldName)
    {
        if (value < 0m)
            throw new BadRequestException($"{fieldName} không được âm.");
    }

    private static void EnsureHistoryProtectedFieldUnchanged<T>(
        T existing,
        T requested,
        string fieldName)
    {
        if (!EqualityComparer<T>.Default.Equals(existing, requested))
        {
            throw new ConflictException(
                $"Không thể sửa {fieldName} vì nguyên liệu đã có lịch sử kho.");
        }
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
