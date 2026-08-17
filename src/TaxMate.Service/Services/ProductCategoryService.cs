using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class ProductCategoryService : IProductCategoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductCategoryRepository _productCategories;
    private readonly IProductRepository _productRepository;
    private readonly IGenericRepository<BusinessProfile> _businessProfiles;

    public ProductCategoryService(
        IUnitOfWork unitOfWork,
        IProductCategoryRepository productCategories,
        IProductRepository productRepository,
        IGenericRepository<BusinessProfile> businessProfiles)
    {
        _unitOfWork = unitOfWork;
        _productCategories = productCategories;
        _productRepository = productRepository;
        _businessProfiles = businessProfiles;
    }

    public async Task<ProductCategoryResponse> CreateAsync(Guid ownerId, Guid businessId, CreateProductCategoryRequest request)
    {
        await EnsureBusinessOwnerAsync(businessId, ownerId);

        // Check limit of 50
        var count = await _productCategories.GetCountByBusinessAsync(businessId);
        if (count >= 50)
        {
            throw new BadRequestException("Mỗi cửa hàng chỉ được tạo tối đa 50 danh mục sản phẩm.");
        }

        var exists = await _productCategories.AnyAsync(x =>
            x.BusinessId == businessId &&
            x.Name.ToLower() == request.Name.ToLower());

        if (exists)
            throw new ConflictException($"Danh mục sản phẩm '{request.Name}' đã tồn tại.");

        var entity = new ProductCategory
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Name = request.Name.Trim(),
            Description = request.Description
        };

        await _productCategories.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponse(entity);
    }

    public async Task<ProductCategoryResponse> UpdateAsync(Guid ownerId, Guid id, UpdateProductCategoryRequest request)
    {
        var entity = await _productCategories.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException("Không tìm thấy danh mục sản phẩm.");

        await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);

        var duplicate = await _productCategories.AnyAsync(x =>
            x.BusinessId == entity.BusinessId &&
            x.Name.ToLower() == request.Name.ToLower() &&
            x.Id != id);

        if (duplicate)
            throw new ConflictException($"Danh mục sản phẩm '{request.Name}' đã tồn tại.");

        entity.Name = request.Name.Trim();
        entity.Description = request.Description;

        _productCategories.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponse(entity);
    }

    public async Task DeleteAsync(Guid ownerId, Guid id, Guid? fallbackProductCategoryId = null, bool forceDelete = false)
    {
        var entity = await _productCategories.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException("Không tìm thấy danh mục sản phẩm.");

        await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);

        // Fetch products using this category
        var products = await _productRepository.FindAsync(x => x.ProductCategoryId == id);
        var productList = products.ToList();

        if (productList.Any())
        {
            if (fallbackProductCategoryId.HasValue)
            {
                var fallbackCategory = await _productCategories.GetByIdAsync(fallbackProductCategoryId.Value);
                if (fallbackCategory is null || fallbackCategory.BusinessId != entity.BusinessId)
                {
                    throw new BadRequestException("Mã danh mục thay thế không hợp lệ.");
                }

                foreach (var product in productList)
                {
                    product.ProductCategoryId = fallbackProductCategoryId.Value;
                    _productRepository.Update(product);
                }
            }
            else if (forceDelete)
            {
                foreach (var product in productList)
                {
                    product.ProductCategoryId = null;
                    _productRepository.Update(product);
                }
            }
            else
            {
                throw new ConflictException("Không thể xóa danh mục này vì có sản phẩm đang sử dụng.");
            }
        }

        _productCategories.Remove(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProductCategoryResponse>> GetByBusinessAsync(Guid ownerId, Guid businessId)
    {
        await EnsureBusinessOwnerAsync(businessId, ownerId);
        var list = await _productCategories.GetByBusinessAsync(businessId);
        return list.Select(MapToResponse);
    }

    public async Task<ProductCategoryResponse> GetByIdAsync(Guid ownerId, Guid id)
    {
        var entity = await _productCategories.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException("Không tìm thấy danh mục sản phẩm.");

        await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);

        return MapToResponse(entity);
    }

    public async Task<List<ProductResponse>> GetActiveProductsUsingCategoryAsync(Guid ownerId, Guid productCategoryId)
    {
        var categoryEntity = await _productCategories.GetByIdAsync(productCategoryId);
        if (categoryEntity is null)
            throw new NotFoundException("Không tìm thấy danh mục sản phẩm.");

        await EnsureBusinessOwnerAsync(categoryEntity.BusinessId, ownerId);

        var products = await _productRepository.FindAsync(x => x.ProductCategoryId == productCategoryId);

        return products.Select(p => new ProductResponse
        {
            Id = p.Id,
            BusinessId = p.BusinessId,
            Name = p.Name,
            ProductCategoryId = p.ProductCategoryId,
            ProductCategoryName = categoryEntity.Name,
            Description = p.Description,
            Unit = p.Unit,
            ImageUrl = p.ImageUrl,
            Status = p.Status,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        }).ToList();
    }

    private async Task EnsureBusinessOwnerAsync(Guid businessId, Guid ownerId)
    {
        var business = await _businessProfiles.GetByIdAsync(businessId);
        if (business is null)
            throw new NotFoundException("Không tìm thấy thông tin cửa hàng.");

        if (business.OwnerId != ownerId)
            throw new UnauthorizedAccessException("Bạn không sở hữu cửa hàng này.");
    }

    private static ProductCategoryResponse MapToResponse(ProductCategory entity)
    {
        return new ProductCategoryResponse
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            Name = entity.Name,
            Description = entity.Description,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
