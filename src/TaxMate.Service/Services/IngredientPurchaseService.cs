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
    private readonly ISupplierRepository _suppliers;

    public IngredientPurchaseService(
        IUnitOfWork unitOfWork,
        IIngredientPurchaseRepository purchases,
        IGenericRepository<BusinessProfile> businesses,
        IIngredientRepository ingredients,
        ISupplierRepository suppliers)
    {
        _unitOfWork = unitOfWork;
        _purchases = purchases;
        _businesses = businesses;
        _ingredients = ingredients;
        _suppliers = suppliers;
    }

    public async Task<IngredientPurchaseResponse> CreateAsync(Guid ownerId, Guid businessId, CreateIngredientPurchaseRequest request)
    {
        await EnsureBusinessOwnerAsync(businessId, ownerId);

        var ingredient = await _ingredients.GetByIdAsync(request.IngredientId);
        if (ingredient is null || ingredient.IsDeleted)
            throw new NotFoundException($"Ingredient with id '{request.IngredientId}' not found or deactivated.");

        string? supplierName = request.SupplierName;
        if (request.SupplierId.HasValue && string.IsNullOrWhiteSpace(supplierName))
        {
            var supplier = await _suppliers.GetByIdAsync(request.SupplierId.Value);
            if (supplier != null)
            {
                supplierName = supplier.Name;
            }
        }

        var entity = new IngredientPurchase
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            IngredientId = request.IngredientId,
            Quantity = request.Quantity,
            TotalCost = request.TotalCost,
            PurchaseDate = request.PurchaseDate.ToUniversalTime(),
            InvoiceNumber = request.InvoiceNumber,
            SupplierId = request.SupplierId,
            SupplierName = supplierName,
            ReceiptImageUrl = request.ReceiptImageUrl,
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

    public async Task<IngredientPurchaseResponse> UpdateAsync(Guid ownerId, Guid id, UpdateIngredientPurchaseRequest request)
    {
        var entity = await _purchases.GetByIdWithDetailsAsync(id);
        if (entity is null)
            throw new NotFoundException($"Ingredient purchase with id '{id}' not found.");

        await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);

        if (entity.IngredientId != request.IngredientId)
        {
            var ingredient = await _ingredients.GetByIdAsync(request.IngredientId);
            if (ingredient is null || ingredient.IsDeleted)
                throw new NotFoundException($"Ingredient with id '{request.IngredientId}' not found or deactivated.");
            entity.IngredientId = request.IngredientId;
        }

        string? supplierName = request.SupplierName;
        if (request.SupplierId.HasValue && string.IsNullOrWhiteSpace(supplierName))
        {
            var supplier = await _suppliers.GetByIdAsync(request.SupplierId.Value);
            if (supplier != null)
            {
                supplierName = supplier.Name;
            }
        }

        entity.Quantity = request.Quantity;
        entity.TotalCost = request.TotalCost;
        entity.PurchaseDate = request.PurchaseDate.ToUniversalTime();
        entity.InvoiceNumber = request.InvoiceNumber;
        entity.SupplierId = request.SupplierId;
        entity.SupplierName = supplierName;
        entity.ReceiptImageUrl = request.ReceiptImageUrl;
        entity.UpdatedAt = DateTime.UtcNow;

        _purchases.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        var details = await _purchases.GetByIdWithDetailsAsync(id);
        if (details is null)
            throw new NotFoundException($"Ingredient purchase with id '{id}' could not be re-loaded.");

        return MapToResponse(details);
    }

    public async Task DeleteAsync(Guid ownerId, Guid id)
    {
        var entity = await _purchases.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException($"Ingredient purchase with id '{id}' not found.");

        await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);

        _purchases.Remove(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<PagedResult<IngredientPurchaseResponse>> GetPagedByBusinessAsync(
        Guid ownerId, Guid businessId, int pageNumber, int pageSize, string? search)
    {
        await EnsureBusinessOwnerAsync(businessId, ownerId);

        var (items, totalCount) = await _purchases.GetPagedByBusinessAsync(businessId, pageNumber, pageSize, search);
        return new PagedResult<IngredientPurchaseResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IngredientPurchaseResponse> GetByIdAsync(Guid ownerId, Guid id)
    {
        var details = await _purchases.GetByIdWithDetailsAsync(id);
        if (details is null)
            throw new NotFoundException($"Ingredient purchase with id '{id}' not found.");

        await EnsureBusinessOwnerAsync(details.BusinessId, ownerId);

        return MapToResponse(details);
    }

    public async Task<IEnumerable<IngredientPurchaseResponse>> CreateBatchAsync(Guid ownerId, Guid businessId, CreateBatchIngredientPurchaseRequest request)
    {
        await EnsureBusinessOwnerAsync(businessId, ownerId);

        string? supplierName = request.SupplierName;
        if (request.SupplierId.HasValue && string.IsNullOrWhiteSpace(supplierName))
        {
            var supplier = await _suppliers.GetByIdAsync(request.SupplierId.Value);
            if (supplier != null)
            {
                supplierName = supplier.Name;
            }
        }

        var responses = new List<IngredientPurchaseResponse>();
        var entitiesToAdd = new List<IngredientPurchase>();

        foreach (var item in request.Items)
        {
            var ingredient = await _ingredients.GetByIdAsync(item.IngredientId);
            if (ingredient is null || ingredient.IsDeleted)
                throw new NotFoundException($"Ingredient with id '{item.IngredientId}' not found or deactivated.");

            var entity = new IngredientPurchase
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                IngredientId = item.IngredientId,
                Quantity = item.Quantity,
                TotalCost = item.TotalCost,
                PurchaseDate = request.PurchaseDate.ToUniversalTime(),
                InvoiceNumber = request.InvoiceNumber,
                SupplierId = request.SupplierId,
                SupplierName = supplierName,
                ReceiptImageUrl = request.ReceiptImageUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            entitiesToAdd.Add(entity);
        }

        foreach (var entity in entitiesToAdd)
        {
            await _purchases.AddAsync(entity);
        }

        await _unitOfWork.SaveChangesAsync();

        foreach (var entity in entitiesToAdd)
        {
            var details = await _purchases.GetByIdWithDetailsAsync(entity.Id);
            if (details is not null)
            {
                responses.Add(MapToResponse(details));
            }
        }

        return responses;
    }

    private async Task EnsureBusinessOwnerAsync(Guid businessId, Guid ownerId)
    {
        var business = await _businesses.GetByIdAsync(businessId);
        if (business is null)
            throw new NotFoundException("Không tìm thấy thông tin cửa hàng.");

        if (business.OwnerId != ownerId)
            throw new UnauthorizedAccessException("Bạn không sở hữu cửa hàng này.");
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
            InvoiceNumber = entity.InvoiceNumber,
            SupplierId = entity.SupplierId,
            SupplierName = entity.Supplier?.Name ?? entity.SupplierName,
            ReceiptImageUrl = entity.ReceiptImageUrl,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
