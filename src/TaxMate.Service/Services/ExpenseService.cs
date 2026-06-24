using AutoMapper;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.Expense;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class ExpenseService : IExpenseService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExpenseRepository _expenses;
    private readonly IExpenseCategoryRepository _expenseCategories;
    private readonly IGenericRepository<BusinessProfile> _businessProfiles;
    private readonly IMapper _mapper;

    public ExpenseService(
        IUnitOfWork unitOfWork,
        IExpenseRepository expenses,
        IExpenseCategoryRepository expenseCategories,
        IGenericRepository<BusinessProfile> businessProfiles,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _expenses = expenses;
        _expenseCategories = expenseCategories;
        _businessProfiles = businessProfiles;
        _mapper = mapper;
    }

    public async Task<ExpenseDTO> CreateAsync(Guid ownerId, Guid businessId, CreateExpenseRequest request)
    {
        await EnsureBusinessOwnerAsync(businessId, ownerId);
        await EnsureCategoryIsValidAsync(request.ExpenseCategoryId, businessId);

        var entity = new Expense
        {
            ExpenseId = Guid.NewGuid(),
            BusinessId = businessId,
            ExpenseCategoryId = request.ExpenseCategoryId,
            ExpenseTitle = request.ExpenseTitle.Trim(),
            Amount = request.Amount,
            ExpenseDate = request.ExpenseDate,
            PaymentMethod = request.PaymentMethod,
            ReceiptImageUrl = request.ReceiptImageUrl,
            Note = request.Note,
            FileUrl = request.FileUrl,
            DueDate = request.DueDate,
            PaidDate = request.PaidDate
        };

        await _expenses.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var created = await _expenses.GetByIdWithCategoryAsync(entity.ExpenseId);
        return _mapper.Map<ExpenseDTO>(created!);
    }

    public async Task<ExpenseDTO> UpdateAsync(Guid ownerId, Guid id, UpdateExpenseRequest request)
    {
        var entity = await _expenses.GetByIdWithCategoryAsync(id);
        if (entity is null)
            throw new NotFoundException("Expense not found.");

        await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);
        await EnsureCategoryIsValidAsync(request.ExpenseCategoryId, entity.BusinessId);

        entity.ExpenseCategoryId = request.ExpenseCategoryId;
        entity.ExpenseTitle = request.ExpenseTitle.Trim();
        entity.Amount = request.Amount;
        entity.ExpenseDate = request.ExpenseDate;
        entity.PaymentMethod = request.PaymentMethod;
        entity.ReceiptImageUrl = request.ReceiptImageUrl;
        entity.Note = request.Note;
        entity.FileUrl = request.FileUrl;
        entity.DueDate = request.DueDate;
        entity.PaidDate = request.PaidDate;

        _expenses.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _expenses.GetByIdWithCategoryAsync(id);
        return _mapper.Map<ExpenseDTO>(updated!);
    }

    public async Task DeleteAsync(Guid ownerId, Guid id)
    {
        var entity = await _expenses.GetByIdAsync(id);
        if (entity is null)
            throw new NotFoundException("Expense not found.");

        await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);

        _expenses.Remove(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<ExpenseDTO> GetByIdAsync(Guid ownerId, Guid id)
    {
        var entity = await _expenses.GetByIdWithCategoryAsync(id);
        if (entity is null)
            throw new NotFoundException("Expense not found.");

        await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);

        return _mapper.Map<ExpenseDTO>(entity);
    }

    public async Task<PagedResult<ExpenseDTO>> GetPagedAsync(
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

        var (items, totalCount) = await _expenses.GetPagedAsync(
            businessId, pageNumber, pageSize, search, fromDate, toDate, categoryId, paymentMethod);

        return new PagedResult<ExpenseDTO>
        {
            Items = _mapper.Map<List<ExpenseDTO>>(items),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<ExpenseSummaryDTO> GetMonthlySummaryAsync(Guid ownerId, Guid businessId, int year, int month)
    {
        await EnsureBusinessOwnerAsync(businessId, ownerId);

        var items = await _expenses.GetMonthlyExpensesAsync(businessId, year, month);

        var grouped = items
            .GroupBy(x => new { x.ExpenseCategoryId, x.ExpenseCategory.CategoryName })
            .Select(g => new ExpenseByCategoryDTO
            {
                ExpenseCategoryId = g.Key.ExpenseCategoryId,
                CategoryName = g.Key.CategoryName,
                Amount = g.Sum(x => x.Amount)
            })
            .ToList();

        return new ExpenseSummaryDTO
        {
            TotalExpense = grouped.Sum(x => x.Amount),
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
        var category = await _expenseCategories.GetByIdAsync(categoryId);
        if (category is null)
            throw new BadRequestException("Invalid expense category.");

        if (category.BusinessId != null && category.BusinessId != businessId)
            throw new BadRequestException("Category does not belong to this business.");
    }
}
