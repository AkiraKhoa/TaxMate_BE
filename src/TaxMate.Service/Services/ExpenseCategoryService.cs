using AutoMapper;
using TaxMate.Model.DTO.ExpenseCategory;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class ExpenseCategoryService : IExpenseCategoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExpenseCategoryRepository _expenseCategories;
    private readonly IGenericRepository<BusinessProfile> _businessProfiles;
    private readonly IMapper _mapper;

    public ExpenseCategoryService(
        IUnitOfWork unitOfWork,
        IExpenseCategoryRepository expenseCategories,
        IGenericRepository<BusinessProfile> businessProfiles,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _expenseCategories = expenseCategories;
        _businessProfiles = businessProfiles;
        _mapper = mapper;
    }

    public async Task<ExpenseCategoryDTO> CreateAsync(Guid ownerId, Guid businessId, CreateExpenseCategoryRequest request)
    {
        await EnsureBusinessOwnerAsync(businessId, ownerId);

        var exists = await _expenseCategories.AnyAsync(x => 
            x.BusinessId == businessId && 
            x.CategoryName.ToLower() == request.CategoryName.ToLower());

        if (exists)
            throw new ConflictException($"Category '{request.CategoryName}' already exists.");

        var entity = new ExpenseCategory
        {
            ExpenseCategoryId = Guid.NewGuid(),
            BusinessId = businessId,
            CategoryName = request.CategoryName.Trim(),
            Description = request.Description,
            IsDefault = false
        };

        await _expenseCategories.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<ExpenseCategoryDTO>(entity);
    }

    public async Task<ExpenseCategoryDTO> UpdateAsync(Guid ownerId, Guid id, UpdateExpenseCategoryRequest request)
    {
        var entity = await _expenseCategories.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException("Expense category not found.");

        if (entity.BusinessId == null)
            throw new BadRequestException("Cannot update global category.");

        await EnsureBusinessOwnerAsync(entity.BusinessId.Value, ownerId);

        var duplicate = await _expenseCategories.AnyAsync(x => 
            x.BusinessId == entity.BusinessId && 
            x.CategoryName.ToLower() == request.CategoryName.ToLower() &&
            x.ExpenseCategoryId != id);

        if (duplicate)
            throw new ConflictException($"Category '{request.CategoryName}' already exists.");

        entity.CategoryName = request.CategoryName.Trim();
        entity.Description = request.Description;

        _expenseCategories.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<ExpenseCategoryDTO>(entity);
    }

    public async Task DeleteAsync(Guid ownerId, Guid id)
    {
        var entity = await _expenseCategories.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException("Expense category not found.");

        if (entity.BusinessId == null)
            throw new BadRequestException("Cannot delete global category.");

        await EnsureBusinessOwnerAsync(entity.BusinessId.Value, ownerId);

        _expenseCategories.Remove(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ExpenseCategoryDTO>> GetByBusinessAsync(Guid ownerId, Guid businessId)
    {
        await EnsureBusinessOwnerAsync(businessId, ownerId);
        var categories = await _expenseCategories.GetCategoriesForBusinessAsync(businessId);
        return _mapper.Map<IEnumerable<ExpenseCategoryDTO>>(categories);
    }

    public async Task<ExpenseCategoryDTO> GetByIdAsync(Guid ownerId, Guid id)
    {
        var entity = await _expenseCategories.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException("Expense category not found.");

        if (entity.BusinessId != null)
        {
            await EnsureBusinessOwnerAsync(entity.BusinessId.Value, ownerId);
        }

        return _mapper.Map<ExpenseCategoryDTO>(entity);
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
