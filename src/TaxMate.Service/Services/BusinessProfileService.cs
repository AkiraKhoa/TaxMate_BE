using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class BusinessProfileService : IBusinessProfileService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBusinessProfileRepository _businessProfiles;
    private readonly IGenericRepository<User> _users;
    private readonly IGenericRepository<BusinessCategory> _businessCategories;
    private readonly IInventoryAdjustmentService _inventoryAdjustments;
    private readonly IPaymentAccountService _paymentAccounts;

    public BusinessProfileService(
        IUnitOfWork unitOfWork,
        IBusinessProfileRepository businessProfiles,
        IGenericRepository<User> users,
        IGenericRepository<BusinessCategory> businessCategories,
        IInventoryAdjustmentService inventoryAdjustments,
        IPaymentAccountService paymentAccounts)
    {
        _unitOfWork = unitOfWork;
        _businessProfiles = businessProfiles;
        _users = users;
        _businessCategories = businessCategories;
        _inventoryAdjustments = inventoryAdjustments;
        _paymentAccounts = paymentAccounts;
    }

    public async Task<BusinessProfileResponse> CreateAsync(
        Guid authenticatedOwnerId,
        CreateBusinessProfileRequest request)
    {
        if (request.OwnerId != Guid.Empty && request.OwnerId != authenticatedOwnerId)
            throw new ForbiddenException();

        // Validate owner exists
        var owner = await _users.GetByIdAsync(authenticatedOwnerId);
        if (owner is null)
            throw new NotFoundException($"Owner with id '{authenticatedOwnerId}' not found.");

        // Validate MainCategoryId if provided
        if (request.MainCategoryId.HasValue)
        {
            var category = await _businessCategories.GetByIdAsync(request.MainCategoryId.Value);
            if (category is null)
                throw new NotFoundException($"Business category with id '{request.MainCategoryId}' not found.");
        }

        var entity = new BusinessProfile
        {
            Id = Guid.NewGuid(),
            OwnerId = authenticatedOwnerId,
            BusinessName = request.BusinessName,
            ProvinceCode = request.ProvinceCode,
            WardCode = request.WardCode,
            Address = request.Address,
            MainCategoryId = request.MainCategoryId,
            PreferElectronicInvoice = request.PreferElectronicInvoice,
            IsStockTrackingEnabled = request.IsStockTrackingEnabled,
            IsActive = true
        };

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _businessProfiles.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();
            await _paymentAccounts.EnsureCashAccountAsync(entity.Id);
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

        // Reload with category
        var created = await _businessProfiles.GetByIdWithCategoryAsync(entity.Id);
        return MapToResponse(created!);
    }

    public async Task<BusinessProfileResponse> UpdateAsync(
        Guid authenticatedOwnerId,
        Guid id,
        UpdateBusinessProfileRequest request)
    {
        var entity = await _businessProfiles.GetByIdWithCategoryAsync(id);
        if (entity is null)
            throw new NotFoundException($"Business profile with id '{id}' not found.");

        EnsureOwner(entity, authenticatedOwnerId);

        if (!entity.IsActive)
            throw new ConflictException($"Business profile with id '{id}' has been deactivated.");

        // Validate MainCategoryId if provided
        if (request.MainCategoryId.HasValue)
        {
            var category = await _businessCategories.GetByIdAsync(request.MainCategoryId.Value);
            if (category is null)
                throw new NotFoundException($"Business category with id '{request.MainCategoryId}' not found.");
        }

        entity.BusinessName = request.BusinessName;
        entity.ProvinceCode = request.ProvinceCode;
        entity.WardCode = request.WardCode;
        entity.Address = request.Address;
        entity.MainCategoryId = request.MainCategoryId;
        entity.PreferElectronicInvoice = request.PreferElectronicInvoice;
        if (request.IsStockTrackingEnabled.HasValue &&
            request.IsStockTrackingEnabled.Value != entity.IsStockTrackingEnabled)
        {
            throw new BadRequestException(
                "Hãy dùng luồng bật/tắt quản lý tồn kho; bật lại cần hoàn tất kiểm kho nhanh.");
        }

        _businessProfiles.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        // Reload with category
        var updated = await _businessProfiles.GetByIdWithCategoryAsync(id);
        return MapToResponse(updated!);
    }

    public async Task<BusinessProfileResponse> ToggleStockTrackingAsync(
        Guid authenticatedOwnerId,
        Guid id,
        ToggleStockTrackingRequest request)
    {
        var entity = await _businessProfiles.GetByIdWithCategoryAsync(id);
        if (entity is null)
            throw new NotFoundException($"Business profile with id '{id}' not found.");

        EnsureOwner(entity, authenticatedOwnerId);

        if (!entity.IsActive)
            throw new ConflictException($"Business profile with id '{id}' has been deactivated.");

        if (request.IsStockTrackingEnabled == entity.IsStockTrackingEnabled)
            return MapToResponse(entity);

        if (request.IsStockTrackingEnabled)
        {
            if (request.Reconciliation is null)
            {
                throw new BadRequestException(
                    "Bật lại quản lý tồn kho cần hoàn tất kiểm kho nhanh.");
            }

            await _inventoryAdjustments.ReconcileAsync(
                authenticatedOwnerId,
                id,
                request.Reconciliation,
                enableStockTracking: true);
            var reconciled = await _businessProfiles.GetByIdWithCategoryAsync(id);
            return MapToResponse(reconciled!);
        }

        // OFF only hides inventory quantities/warnings in the UX. It never
        // deletes movements, resets caches, or disables source-ledger writes.
        entity.IsStockTrackingEnabled = false;
        _businessProfiles.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _businessProfiles.GetByIdWithCategoryAsync(id);
        return MapToResponse(updated!);
    }

    public async Task DeactivateAsync(Guid authenticatedOwnerId, Guid id)
    {
        var entity = await _businessProfiles.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException($"Business profile with id '{id}' not found.");

        EnsureOwner(entity, authenticatedOwnerId);

        if (!entity.IsActive)
            throw new ConflictException($"Business profile with id '{id}' is already deactivated.");

        entity.IsActive = false;
        _businessProfiles.Update(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<PagedResult<BusinessProfileResponse>> GetPagedAsync(
        Guid ownerId, int pageNumber, int pageSize, string? search)
    {
        var (items, totalCount) = await _businessProfiles
            .GetPagedByOwnerAsync(ownerId, pageNumber, pageSize, search);

        return new PagedResult<BusinessProfileResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<BusinessProfileResponse> GetByIdAsync(
        Guid authenticatedOwnerId,
        Guid id)
    {
        var entity = await _businessProfiles.GetByIdWithCategoryAsync(id);
        if (entity is null)
            throw new NotFoundException($"Business profile with id '{id}' not found.");

        EnsureOwner(entity, authenticatedOwnerId);

        return MapToResponse(entity);
    }

    private static void EnsureOwner(BusinessProfile entity, Guid ownerId)
    {
        if (entity.OwnerId != ownerId)
            throw new ForbiddenException();
    }

    private static BusinessProfileResponse MapToResponse(BusinessProfile entity)
    {
        return new BusinessProfileResponse
        {
            Id = entity.Id,
            OwnerId = entity.OwnerId,
            BusinessName = entity.BusinessName,
            ProvinceCode = entity.ProvinceCode,
            WardCode = entity.WardCode,
            Address = entity.Address,
            MainCategoryId = entity.MainCategoryId,
            MainCategoryName = entity.MainCategory?.Name,
            PreferElectronicInvoice = entity.PreferElectronicInvoice,
            IsStockTrackingEnabled = entity.IsStockTrackingEnabled,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
