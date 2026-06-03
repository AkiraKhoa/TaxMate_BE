using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class BusinessProfileService : IBusinessProfileService
{
    private readonly IUnitOfWork _unitOfWork;

    public BusinessProfileService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<BusinessProfileResponse> CreateAsync(CreateBusinessProfileRequest request)
    {
        // Validate owner exists
        var owner = await _unitOfWork.Repository<User>().GetByIdAsync(request.OwnerId);
        if (owner is null)
            throw new Exception($"Owner with id '{request.OwnerId}' not found.");

        // Validate MainCategoryId if provided
        if (request.MainCategoryId.HasValue)
        {
            var category = await _unitOfWork.Repository<BusinessCategory>()
                .GetByIdAsync(request.MainCategoryId.Value);
            if (category is null)
                throw new Exception($"Business category with id '{request.MainCategoryId}' not found.");
        }

        var entity = new BusinessProfile
        {
            Id = Guid.NewGuid(),
            OwnerId = request.OwnerId,
            BusinessName = request.BusinessName,
            ProvinceCode = request.ProvinceCode,
            WardCode = request.WardCode,
            Address = request.Address,
            MainCategoryId = request.MainCategoryId,
            PreferElectronicInvoice = request.PreferElectronicInvoice,
            IsActive = true
        };

        await _unitOfWork.BusinessProfiles.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        // Reload with category
        var created = await _unitOfWork.BusinessProfiles.GetByIdWithCategoryAsync(entity.Id);
        return MapToResponse(created!);
    }

    public async Task<BusinessProfileResponse> UpdateAsync(Guid id, UpdateBusinessProfileRequest request)
    {
        var entity = await _unitOfWork.BusinessProfiles.GetByIdWithCategoryAsync(id);
        if (entity is null)
            throw new Exception($"Business profile with id '{id}' not found.");

        if (!entity.IsActive)
            throw new Exception($"Business profile with id '{id}' has been deactivated.");

        // Validate MainCategoryId if provided
        if (request.MainCategoryId.HasValue)
        {
            var category = await _unitOfWork.Repository<BusinessCategory>()
                .GetByIdAsync(request.MainCategoryId.Value);
            if (category is null)
                throw new Exception($"Business category with id '{request.MainCategoryId}' not found.");
        }

        entity.BusinessName = request.BusinessName;
        entity.ProvinceCode = request.ProvinceCode;
        entity.WardCode = request.WardCode;
        entity.Address = request.Address;
        entity.MainCategoryId = request.MainCategoryId;
        entity.PreferElectronicInvoice = request.PreferElectronicInvoice;

        _unitOfWork.BusinessProfiles.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        // Reload with category
        var updated = await _unitOfWork.BusinessProfiles.GetByIdWithCategoryAsync(id);
        return MapToResponse(updated!);
    }

    public async Task DeactivateAsync(Guid id)
    {
        var entity = await _unitOfWork.BusinessProfiles.GetByIdAsync(id);
        if (entity is null)
            throw new Exception($"Business profile with id '{id}' not found.");

        if (!entity.IsActive)
            throw new Exception($"Business profile with id '{id}' is already deactivated.");

        entity.IsActive = false;
        _unitOfWork.BusinessProfiles.Update(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<PagedResult<BusinessProfileResponse>> GetPagedAsync(
        Guid ownerId, int pageNumber, int pageSize, string? search)
    {
        var (items, totalCount) = await _unitOfWork.BusinessProfiles
            .GetPagedByOwnerAsync(ownerId, pageNumber, pageSize, search);

        return new PagedResult<BusinessProfileResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<BusinessProfileResponse> GetByIdAsync(Guid id)
    {
        var entity = await _unitOfWork.BusinessProfiles.GetByIdWithCategoryAsync(id);
        if (entity is null)
            throw new Exception($"Business profile with id '{id}' not found.");

        return MapToResponse(entity);
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
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
