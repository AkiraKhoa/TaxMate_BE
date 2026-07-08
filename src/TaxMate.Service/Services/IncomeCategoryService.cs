using AutoMapper;
using TaxMate.Model.DTO.IncomeCategory;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class IncomeCategoryService : IIncomeCategoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIncomeCategoryRepository _incomeCategories;
    private readonly IGenericRepository<BusinessProfile> _businessProfiles;
    private readonly IMapper _mapper;

    public IncomeCategoryService(
        IUnitOfWork unitOfWork,
        IIncomeCategoryRepository incomeCategories,
        IGenericRepository<BusinessProfile> businessProfiles,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _incomeCategories = incomeCategories;
        _businessProfiles = businessProfiles;
        _mapper = mapper;
    }

    public async Task<IncomeCategoryDTO> CreateAsync(Guid ownerId, Guid businessId, CreateIncomeCategoryRequest request)
    {
        await EnsureBusinessOwnerAsync(businessId, ownerId);

        var exists = await _incomeCategories.AnyAsync(x => 
            x.BusinessId == businessId && 
            x.CategoryName.ToLower() == request.CategoryName.ToLower());

        if (exists)
            throw new ConflictException($"Category '{request.CategoryName}' already exists.");

        var entity = new IncomeCategory
        {
            IncomeCategoryId = Guid.NewGuid(),
            BusinessId = businessId,
            CategoryName = request.CategoryName.Trim(),
            Description = request.Description,
            IsDefault = false
        };

        await _incomeCategories.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<IncomeCategoryDTO>(entity);
    }

    public async Task<IncomeCategoryDTO> UpdateAsync(Guid ownerId, Guid id, UpdateIncomeCategoryRequest request)
    {
        var entity = await _incomeCategories.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException("Income category not found.");

        if (entity.BusinessId == null)
            throw new BadRequestException("Cannot update global category.");

        await EnsureBusinessOwnerAsync(entity.BusinessId.Value, ownerId);

        var duplicate = await _incomeCategories.AnyAsync(x => 
            x.BusinessId == entity.BusinessId && 
            x.CategoryName.ToLower() == request.CategoryName.ToLower() &&
            x.IncomeCategoryId != id);

        if (duplicate)
            throw new ConflictException($"Category '{request.CategoryName}' already exists.");

        entity.CategoryName = request.CategoryName.Trim();
        entity.Description = request.Description;

        _incomeCategories.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<IncomeCategoryDTO>(entity);
    }

    public async Task DeleteAsync(Guid ownerId, Guid id)
    {
        var entity = await _incomeCategories.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException("Income category not found.");

        if (entity.BusinessId == null)
            throw new BadRequestException("Cannot delete global category.");

        await EnsureBusinessOwnerAsync(entity.BusinessId.Value, ownerId);

        _incomeCategories.Remove(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<IncomeCategoryDTO>> GetByBusinessAsync(Guid ownerId, Guid businessId)
    {
        await EnsureBusinessOwnerAsync(businessId, ownerId);
        var categories = await _incomeCategories.GetCategoriesForBusinessAsync(businessId);
        return _mapper.Map<IEnumerable<IncomeCategoryDTO>>(categories);
    }

    public async Task<IncomeCategoryDTO> GetByIdAsync(Guid ownerId, Guid id)
    {
        var entity = await _incomeCategories.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException("Income category not found.");

        if (entity.BusinessId != null)
        {
            await EnsureBusinessOwnerAsync(entity.BusinessId.Value, ownerId);
        }

        return _mapper.Map<IncomeCategoryDTO>(entity);
    }

    private async Task EnsureBusinessOwnerAsync(Guid businessId, Guid ownerId)
    {
        var business = await _businessProfiles.GetByIdAsync(businessId);
        if (business is null)
            throw new NotFoundException("Business profile not found.");

        if (business.OwnerId != ownerId)
            throw new UnauthorizedAccessException("You do not own this business.");
    }
}
