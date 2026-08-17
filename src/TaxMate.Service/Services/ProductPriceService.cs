using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class ProductPriceService : IProductPriceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductPriceRepository _productPrices;
    private readonly IProductRepository _products;
    private readonly IGenericRepository<BusinessProfile> _businessProfiles;

    public ProductPriceService(
        IUnitOfWork unitOfWork,
        IProductPriceRepository productPrices,
        IProductRepository products,
        IGenericRepository<BusinessProfile> businessProfiles)
    {
        _unitOfWork = unitOfWork;
        _productPrices = productPrices;
        _products = products;
        _businessProfiles = businessProfiles;
    }

    public async Task<ProductPriceResponse> CreateAsync(
        Guid ownerId,
        Guid productId,
        CreateProductPriceRequest request)
    {
        await EnsureProductOwnerAsync(ownerId, productId);

        if (request.Price <= 0)
            throw new BadRequestException("Số tiền phải lớn hơn 0.");

        // Upsert: one price per product per calendar day — update if it already exists.
        var existing = await _productPrices.FindByProductIdAndApplyDateAsync(
            productId, request.ApplyDate);

        if (existing is not null)
        {
            existing.Price = request.Price;
            _productPrices.Update(existing);
            await _unitOfWork.SaveChangesAsync();
            return MapToResponse(existing);
        }

        var entity = new ProductPrice
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Price = request.Price,
            ApplyDate = request.ApplyDate
        };

        await _productPrices.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponse(entity);
    }

    public async Task<ProductPriceResponse> UpdateAsync(
        Guid ownerId,
        Guid id,
        UpdateProductPriceRequest request)
    {
        var entity = await _productPrices.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException($"Sản phẩm với mã '{id}' không tồn tại.");

        await EnsureProductOwnerAsync(ownerId, entity.ProductId);

        if (request.Price <= 0)
            throw new BadRequestException("Giá sản phẩm phải lớn hơn 0.");

        if (await _productPrices.ExistsDuplicateApplyDateAsync(
                entity.ProductId, request.ApplyDate, id))
            throw new ConflictException("Giá đã tồn tại trong ngày đó.");

        entity.Price = request.Price;
        entity.ApplyDate = request.ApplyDate;

        _productPrices.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponse(entity);
    }

    public async Task DeleteAsync(Guid ownerId, Guid id)
    {
        var entity = await _productPrices.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException($"Product price with id '{id}' not found.");

        await EnsureProductOwnerAsync(ownerId, entity.ProductId);

        _productPrices.Remove(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProductPriceResponse>> GetByProductIdAsync(
        Guid ownerId,
        Guid productId)
    {
        await EnsureProductOwnerAsync(ownerId, productId);

        var items = await _productPrices.GetByProductIdAsync(productId);
        return items.Select(MapToResponse);
    }

    public async Task<ProductPriceResponse> GetByIdAsync(Guid ownerId, Guid id)
    {
        var entity = await _productPrices.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException($"Sản phẩm với mã '{id}' không tồn tại.");

        await EnsureProductOwnerAsync(ownerId, entity.ProductId);

        return MapToResponse(entity);
    }

    private async Task EnsureProductOwnerAsync(Guid ownerId, Guid productId)
    {
        var product = await _products.GetByIdAsync(productId);
        if (product is null || product.IsDeleted)
            throw new NotFoundException($"Sản phẩm với mã '{productId}' không tồn tại.");

        var business = await _businessProfiles.GetByIdAsync(product.BusinessId);
        if (business is null)
            throw new NotFoundException("Không tìm thấy hồ sơ doanh nghiệp.");

        if (business.OwnerId != ownerId)
            throw new UnauthorizedAccessException("Bạn không phải là chủ sở hữu của doanh nghiệp này.");
    }

    private static ProductPriceResponse MapToResponse(ProductPrice entity)
    {
        return new ProductPriceResponse
        {
            Id = entity.Id,
            ProductId = entity.ProductId,
            Price = entity.Price,
            ApplyDate = entity.ApplyDate,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
