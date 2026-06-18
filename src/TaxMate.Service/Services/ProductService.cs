using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class ProductService : IProductService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductRepository _products;
    private readonly IGenericRepository<BusinessProfile> _businessProfiles;

    public ProductService(
        IUnitOfWork unitOfWork,
        IProductRepository products,
        IGenericRepository<BusinessProfile> businessProfiles)
    {
        _unitOfWork = unitOfWork;
        _products = products;
        _businessProfiles = businessProfiles;
    }

    public async Task<ProductResponse> CreateAsync(
        Guid ownerId,
        Guid businessId,
        CreateProductRequest request)
    {
        await EnsureBusinessOwnerAsync(businessId, ownerId);

        var exists = await _products.AnyAsync(x =>
            x.BusinessId == businessId
            && x.Name.ToLower() == request.Name.ToLower());

        if (exists)
            throw new ConflictException($"Product with name '{request.Name}' already exists in this business.");

        var entity = new Product
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Name = request.Name.Trim(),
            Category = request.Category,
            Description = request.Description,
            Unit = request.Unit,
            ImageUrl = request.ImageUrl,
            Status = ProductStatus.Active
        };

        await _products.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponse(entity);
    }

    public async Task<ProductResponse> UpdateAsync(
        Guid ownerId,
        Guid id,
        UpdateProductRequest request)
    {
        var entity = await _products.GetByIdWithPricesAsync(id);
        if (entity is null)
            throw new NotFoundException($"Product with id '{id}' not found.");

        await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);

        var duplicate = await _products.AnyAsync(x =>
            x.BusinessId == entity.BusinessId
            && x.Name.ToLower() == request.Name.ToLower()
            && x.Id != id);

        if (duplicate)
            throw new ConflictException($"Product with name '{request.Name}' already exists in this business.");

        entity.Name = request.Name.Trim();
        entity.Category = request.Category;
        entity.Description = request.Description;
        entity.Unit = request.Unit;
        entity.ImageUrl = request.ImageUrl;

        _products.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponse(entity);
    }

    public async Task<ProductResponse> ToggleStatusAsync(Guid ownerId, Guid id)
    {
        var entity = await _products.GetByIdWithPricesAsync(id);
        if (entity is null)
            throw new NotFoundException($"Product with id '{id}' not found.");

        await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);

        entity.Status = entity.Status == ProductStatus.Active
            ? ProductStatus.Inactive
            : ProductStatus.Active;

        _products.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponse(entity);
    }

    public async Task<PagedResult<ProductResponse>> GetPagedByBusinessAsync(
        Guid ownerId,
        Guid businessId,
        int pageNumber,
        int pageSize,
        string? search,
        string? status,
        ProductCategory? category)
    {
        await EnsureBusinessOwnerAsync(businessId, ownerId);

        var (items, totalCount) = await _products.GetPagedByBusinessAsync(
            businessId, pageNumber, pageSize, search, status, category);

        return new PagedResult<ProductResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<ProductResponse> GetByIdAsync(Guid ownerId, Guid id)
    {
        var entity = await _products.GetByIdWithPricesAsync(id);
        if (entity is null)
            throw new NotFoundException($"Product with id '{id}' not found.");

        await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);

        return MapToResponse(entity);
    }

    private async Task<BusinessProfile> EnsureBusinessOwnerAsync(Guid businessId, Guid ownerId)
    {
        var business = await _businessProfiles.GetByIdAsync(businessId);
        if (business is null)
            throw new NotFoundException("Business profile not found.");

        if (business.OwnerId != ownerId)
            throw new UnauthorizedAccessException("You do not own this business.");

        return business;
    }

    private static ProductResponse MapToResponse(Product entity)
    {
        var now = DateTime.UtcNow;
        var currentPrice = entity.ProductPrices
            .Where(p => p.ApplyDate <= now)
            .OrderByDescending(p => p.ApplyDate)
            .Select(p => p.Price)
            .FirstOrDefault();

        return new ProductResponse
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            Name = entity.Name,
            Category = entity.Category,
            Description = entity.Description,
            Unit = entity.Unit,
            ImageUrl = entity.ImageUrl,
            Status = entity.Status,
            CurrentPrice = currentPrice == 0 ? null : currentPrice,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
