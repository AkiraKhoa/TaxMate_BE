using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class SupplierService : ISupplierService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISupplierRepository _suppliers;
    private readonly IGenericRepository<BusinessProfile> _businessProfiles;

    public SupplierService(
        IUnitOfWork unitOfWork,
        ISupplierRepository suppliers,
        IGenericRepository<BusinessProfile> businessProfiles)
    {
        _unitOfWork = unitOfWork;
        _suppliers = suppliers;
        _businessProfiles = businessProfiles;
    }

    public async Task<SupplierResponse> CreateAsync(Guid ownerId, Guid businessId, CreateSupplierRequest request)
    {
        await EnsureBusinessOwnerAsync(businessId, ownerId);

        var count = await _suppliers.GetCountByBusinessAsync(businessId);
        if (count >= 100)
        {
            throw new BadRequestException("Mỗi cửa hàng chỉ được tạo tối đa 100 nhà cung cấp.");
        }

        var exists = await _suppliers.AnyAsync(x =>
            x.BusinessId == businessId &&
            x.Name.ToLower() == request.Name.ToLower());

        if (exists)
            throw new ConflictException($"Nhà cung cấp '{request.Name}' đã tồn tại.");

        var entity = new Supplier
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Name = request.Name.Trim(),
            ContactName = request.ContactName?.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            Address = request.Address?.Trim(),
            Note = request.Note?.Trim()
        };

        await _suppliers.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponse(entity);
    }

    public async Task<SupplierResponse> UpdateAsync(Guid ownerId, Guid id, UpdateSupplierRequest request)
    {
        var entity = await _suppliers.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException("Không tìm thấy nhà cung cấp.");

        await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);

        var duplicate = await _suppliers.AnyAsync(x =>
            x.BusinessId == entity.BusinessId &&
            x.Name.ToLower() == request.Name.ToLower() &&
            x.Id != id);

        if (duplicate)
            throw new ConflictException($"Nhà cung cấp '{request.Name}' đã tồn tại.");

        entity.Name = request.Name.Trim();
        entity.ContactName = request.ContactName?.Trim();
        entity.PhoneNumber = request.PhoneNumber?.Trim();
        entity.Address = request.Address?.Trim();
        entity.Note = request.Note?.Trim();

        _suppliers.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponse(entity);
    }

    public async Task DeleteAsync(Guid ownerId, Guid id)
    {
        var entity = await _suppliers.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException("Không tìm thấy nhà cung cấp.");

        await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);

        _suppliers.Remove(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<SupplierResponse>> GetByBusinessAsync(Guid ownerId, Guid businessId)
    {
        await EnsureBusinessOwnerAsync(businessId, ownerId);
        var list = await _suppliers.GetByBusinessAsync(businessId);
        return list.Select(MapToResponse);
    }

    public async Task<SupplierResponse> GetByIdAsync(Guid ownerId, Guid id)
    {
        var entity = await _suppliers.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException("Không tìm thấy nhà cung cấp.");

        await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);

        return MapToResponse(entity);
    }

    private async Task EnsureBusinessOwnerAsync(Guid businessId, Guid ownerId)
    {
        var business = await _businessProfiles.GetByIdAsync(businessId);
        if (business is null)
            throw new NotFoundException("Không tìm thấy thông tin cửa hàng.");

        if (business.OwnerId != ownerId)
            throw new UnauthorizedAccessException("Bạn không sở hữu cửa hàng này.");
    }

    private static SupplierResponse MapToResponse(Supplier entity)
    {
        return new SupplierResponse
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            Name = entity.Name,
            ContactName = entity.ContactName,
            PhoneNumber = entity.PhoneNumber,
            Address = entity.Address,
            Note = entity.Note,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
