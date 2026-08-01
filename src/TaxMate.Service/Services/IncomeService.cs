using AutoMapper;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.Income;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class IncomeService : IIncomeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIncomeRepository _incomes;
    private readonly IIncomeCategoryRepository _incomeCategories;
    private readonly IGenericRepository<BusinessProfile> _businessProfiles;
    private readonly IMapper _mapper;

    public IncomeService(
        IUnitOfWork unitOfWork,
        IIncomeRepository incomes,
        IIncomeCategoryRepository incomeCategories,
        IGenericRepository<BusinessProfile> businessProfiles,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _incomes = incomes;
        _incomeCategories = incomeCategories;
        _businessProfiles = businessProfiles;
        _mapper = mapper;
    }

    public async Task<IncomeDTO> CreateAsync(Guid ownerId, Guid businessId, CreateIncomeRequest request)
    {
        await EnsureBusinessOwnerAsync(businessId, ownerId);
        await EnsureCategoryIsValidAsync(request.IncomeCategoryId, businessId);

        var entity = new Income
        {
            IncomeId = Guid.NewGuid(),
            BusinessId = businessId,
            IncomeCategoryId = request.IncomeCategoryId,
            IncomeTitle = request.IncomeTitle.Trim(),
            Amount = request.Amount,
            IncomeDate = request.IncomeDate,
            PaymentMethod = request.PaymentMethod,
            ReceiptImageUrl = request.ReceiptImageUrl,
            Note = request.Note,
            FileUrl = request.FileUrl,
            DueDate = request.DueDate,
            ReceivedDate = request.ReceivedDate
        };

        await _incomes.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var created = await _incomes.GetByIdWithCategoryAsync(entity.IncomeId);
        return _mapper.Map<IncomeDTO>(created!);
    }

    public async Task<IncomeDTO> UpdateAsync(Guid ownerId, Guid id, UpdateIncomeRequest request)
    {
        var entity = await _incomes.GetByIdWithCategoryAsync(id);
        if (entity is null)
            throw new NotFoundException("Income not found.");

        await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);
        await EnsureCategoryIsValidAsync(request.IncomeCategoryId, entity.BusinessId);

        entity.IncomeCategoryId = request.IncomeCategoryId;
        entity.IncomeTitle = request.IncomeTitle.Trim();
        entity.Amount = request.Amount;
        entity.IncomeDate = request.IncomeDate;
        entity.PaymentMethod = request.PaymentMethod;
        entity.ReceiptImageUrl = request.ReceiptImageUrl;
        entity.Note = request.Note;
        entity.FileUrl = request.FileUrl;
        entity.DueDate = request.DueDate;
        entity.ReceivedDate = request.ReceivedDate;

        _incomes.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _incomes.GetByIdWithCategoryAsync(id);
        return _mapper.Map<IncomeDTO>(updated!);
    }

    public async Task DeleteAsync(Guid ownerId, Guid id)
    {
        var entity = await _incomes.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException("Income not found.");

        await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);

        _incomes.Remove(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IncomeDTO> GetByIdAsync(Guid ownerId, Guid id)
    {
        var entity = await _incomes.GetByIdWithCategoryAsync(id);
        if (entity is null)
            throw new NotFoundException("Income not found.");

        await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);

        return _mapper.Map<IncomeDTO>(entity);
    }

    public async Task<PagedResult<IncomeDTO>> GetPagedAsync(
        Guid ownerId, 
        Guid businessId, 
        int pageNumber, 
        int pageSize, 
        string? search, 
        DateTime? fromDate, 
        DateTime? toDate, 
        Guid? categoryId, 
        string? paymentMethod)
    {
        await EnsureBusinessOwnerAsync(businessId, ownerId);

        var (items, totalCount) = await _incomes.GetPagedAsync(
            businessId, pageNumber, pageSize, search, fromDate, toDate, categoryId, paymentMethod);

        return new PagedResult<IncomeDTO>
        {
            Items = _mapper.Map<List<IncomeDTO>>(items),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IncomeSummaryDTO> GetMonthlySummaryAsync(Guid ownerId, Guid businessId, int year, int month)
    {
        await EnsureBusinessOwnerAsync(businessId, ownerId);

        var items = await _incomes.GetMonthlyIncomesAsync(businessId, year, month);

        var grouped = items
            .GroupBy(x => new { x.IncomeCategoryId, x.IncomeCategory.CategoryName })
            .Select(g => new IncomeByCategoryDTO
            {
                IncomeCategoryId = g.Key.IncomeCategoryId,
                CategoryName = g.Key.CategoryName,
                Amount = g.Sum(x => x.Amount)
            })
            .ToList();

        return new IncomeSummaryDTO
        {
            TotalIncome = grouped.Sum(x => x.Amount),
            ByCategories = grouped
        };
    }

    private async Task EnsureBusinessOwnerAsync(Guid businessId, Guid ownerId)
    {
        var business = await _businessProfiles.GetByIdAsync(businessId);
        if (business is null)
            throw new NotFoundException("Business profile not found.");

        if (business.OwnerId != ownerId)
            throw new UnauthorizedAccessException("You do not own this business.");
    }

    private async Task EnsureCategoryIsValidAsync(Guid categoryId, Guid businessId)
    {
        var category = await _incomeCategories.GetByIdAsync(categoryId);
        if (category is null)
            throw new BadRequestException("Invalid income category.");

        if (category.BusinessId != null && category.BusinessId != businessId)
            throw new BadRequestException("Category does not belong to this business.");
    }
}
