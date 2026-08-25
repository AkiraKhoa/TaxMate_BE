using AutoMapper;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.Expense;
using TaxMate.Model.DTO.MoneyMovement;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class ExpenseService : IExpenseService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExpenseRepository _expenses;
    private readonly IExpenseCategoryRepository _expenseCategories;
    private readonly IGenericRepository<BusinessProfile> _businessProfiles;
    private readonly IPaymentAccountService _paymentAccounts;
    private readonly IMoneyMovementService _moneyMovements;
    private readonly ITaxPeriodMutationGuard _periodGuard;
    private readonly IMapper _mapper;

    public ExpenseService(
        IUnitOfWork unitOfWork,
        IExpenseRepository expenses,
        IExpenseCategoryRepository expenseCategories,
        IGenericRepository<BusinessProfile> businessProfiles,
        IPaymentAccountService paymentAccounts,
        IMoneyMovementService moneyMovements,
        ITaxPeriodMutationGuard periodGuard,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _expenses = expenses;
        _expenseCategories = expenseCategories;
        _businessProfiles = businessProfiles;
        _paymentAccounts = paymentAccounts;
        _moneyMovements = moneyMovements;
        _periodGuard = periodGuard;
        _mapper = mapper;
    }

    public async Task<ExpenseDTO> CreateAsync(Guid ownerId, Guid businessId, CreateExpenseRequest request)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await EnsureBusinessOwnerAsync(businessId, ownerId);
            await GuardCreateDatesAsync(ownerId, businessId, request.ExpenseDate, request.PaidDate);
            var payment = await ResolvePaymentAsync(ownerId, businessId, request.PaidDate, request.PaymentMethod, request.PaymentAccountId);

            Guid categoryId;
            if (request.ExpenseCategoryId.HasValue && request.ExpenseCategoryId.Value != Guid.Empty)
            {
                await EnsureCategoryIsValidAsync(request.ExpenseCategoryId.Value, businessId);
                categoryId = request.ExpenseCategoryId.Value;
            }
            else
            {
                var defaultCat = await _expenseCategories.FirstOrDefaultAsync(x => x.BusinessId == businessId && x.CategoryName == "Chưa phân loại");
                if (defaultCat == null)
                {
                    defaultCat = new ExpenseCategory
                    {
                        ExpenseCategoryId = Guid.NewGuid(),
                        BusinessId = businessId,
                        CategoryName = "Chưa phân loại",
                        Description = "Chi phí vận hành khác",
                        IsDefault = true
                    };
                    await _expenseCategories.AddAsync(defaultCat);
                }
                categoryId = defaultCat.ExpenseCategoryId;
            }

            var expenseId = Guid.NewGuid();
            var entity = new Expense
            {
                ExpenseId = expenseId,
                BusinessId = businessId,
                ExpenseCategoryId = categoryId,
                VoucherNumber = AccountingDocumentNumber.FromSource("PC", expenseId),
                ExpenseTitle = request.ExpenseTitle.Trim(),
                Amount = request.Amount,
                ExpenseDate = request.ExpenseDate,
                PaymentMethod = payment.PaymentMethod,
                ReceiptImageUrl = request.ReceiptImageUrl,
                Note = request.Note,
                FileUrl = request.FileUrl,
                DueDate = request.DueDate,
                PaidDate = request.PaidDate,
                SupplierId = request.SupplierId
            };

            await _expenses.AddAsync(entity);
            await SyncMovementAsync(ownerId, entity, payment);
            await _unitOfWork.SaveChangesAsync();
            var created = await _expenses.GetByIdWithCategoryAsync(entity.ExpenseId);
            await _unitOfWork.CommitTransactionAsync();
            return _mapper.Map<ExpenseDTO>(created!);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<ExpenseDTO> UpdateAsync(Guid ownerId, Guid id, UpdateExpenseRequest request)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var entity = await _expenses.GetByIdWithCategoryAsync(id);
            if (entity is null)
                throw new NotFoundException("Expense not found.");
            EnsureManualExpense(entity);

            await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);
            await EnsureCategoryIsValidAsync(request.ExpenseCategoryId, entity.BusinessId);
            await GuardUpdateDatesAsync(ownerId, entity.BusinessId, entity.ExpenseDate, request.ExpenseDate, entity.PaidDate, request.PaidDate);
            var payment = await ResolvePaymentAsync(ownerId, entity.BusinessId, request.PaidDate, request.PaymentMethod, request.PaymentAccountId);

            entity.ExpenseCategoryId = request.ExpenseCategoryId;
            entity.ExpenseTitle = request.ExpenseTitle.Trim();
            entity.Amount = request.Amount;
            entity.ExpenseDate = request.ExpenseDate;
            entity.PaymentMethod = payment.PaymentMethod;
            entity.ReceiptImageUrl = request.ReceiptImageUrl;
            entity.Note = request.Note;
            entity.FileUrl = request.FileUrl;
            entity.DueDate = request.DueDate;
            entity.PaidDate = request.PaidDate;
            entity.SupplierId = request.SupplierId;

            _expenses.Update(entity);
            await SyncMovementAsync(ownerId, entity, payment);
            await _unitOfWork.SaveChangesAsync();
            var updated = await _expenses.GetByIdWithCategoryAsync(id);
            await _unitOfWork.CommitTransactionAsync();
            return _mapper.Map<ExpenseDTO>(updated!);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task DeleteAsync(Guid ownerId, Guid id)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var entity = await _expenses.GetByIdAsync(id);
            if (entity is null)
                throw new NotFoundException("Expense not found.");
            EnsureManualExpense(entity);

            await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);
            await _periodGuard.EnsureCanDeleteAsync(ownerId, entity.BusinessId, entity.ExpenseDate);
            if (entity.PaidDate.HasValue && entity.PaidDate.Value != entity.ExpenseDate)
                await _periodGuard.EnsureCanDeleteAsync(ownerId, entity.BusinessId, entity.PaidDate.Value);

            await _moneyMovements.DeleteAsync(ownerId, entity.BusinessId, MoneyMovementTypes.ExpenseOut, entity.ExpenseId);
            _expenses.Remove(entity);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
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

    private async Task<ResolvedPayment> ResolvePaymentAsync(Guid ownerId, Guid businessId, DateTime? paidDate, string? paymentMethod, Guid? paymentAccountId)
    {
        if (!paidDate.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(paymentMethod) || paymentAccountId.HasValue)
                throw new BadRequestException("Payment information is only allowed after the expense is paid.");
            return new ResolvedPayment(null, null);
        }

        if (string.Equals(paymentMethod?.Trim(), PaymentMethods.Cash, StringComparison.OrdinalIgnoreCase))
        {
            if (paymentAccountId.HasValue)
                throw new BadRequestException("Cash expenses use the system cash account automatically.");
            var cash = await _paymentAccounts.GetCashByBusinessIdAsync(ownerId, businessId);
            return new ResolvedPayment(PaymentMethods.Cash, cash.PaymentAccountId);
        }

        if (string.Equals(paymentMethod?.Trim(), PaymentMethods.Transfer, StringComparison.OrdinalIgnoreCase))
        {
            if (!paymentAccountId.HasValue)
                throw new BadRequestException("A bank account is required for transfer expenses.");
            return new ResolvedPayment(PaymentMethods.Transfer, paymentAccountId);
        }

        throw new BadRequestException("Payment method must be Cash or Transfer.");
    }

    private async Task SyncMovementAsync(Guid ownerId, Expense expense, ResolvedPayment payment)
    {
        if (!expense.PaidDate.HasValue)
        {
            await _moneyMovements.DeleteAsync(ownerId, expense.BusinessId, MoneyMovementTypes.ExpenseOut, expense.ExpenseId);
            return;
        }

        await _moneyMovements.SyncAsync(new MoneyMovementWriteRequest
        {
            OwnerId = ownerId,
            BusinessId = expense.BusinessId,
            PaymentAccountId = payment.PaymentAccountId!.Value,
            PaymentMethod = payment.PaymentMethod!,
            MovementType = MoneyMovementTypes.ExpenseOut,
            Amount = expense.Amount,
            MovementDate = expense.PaidDate.Value,
            DocumentNumber = expense.VoucherNumber,
            Description = $"Chi khác: {expense.ExpenseTitle}",
            ReferenceId = expense.ExpenseId
        });
    }

    private async Task GuardCreateDatesAsync(Guid ownerId, Guid businessId, DateTime expenseDate, DateTime? paidDate)
    {
        await _periodGuard.EnsureCanCreateAsync(ownerId, businessId, expenseDate);
        if (paidDate.HasValue && paidDate.Value != expenseDate)
            await _periodGuard.EnsureCanCreateAsync(ownerId, businessId, paidDate.Value);
    }

    private async Task GuardUpdateDatesAsync(Guid ownerId, Guid businessId, DateTime oldExpenseDate, DateTime newExpenseDate, DateTime? oldPaidDate, DateTime? newPaidDate)
    {
        await _periodGuard.EnsureCanMutateAsync(ownerId, businessId, oldExpenseDate, newExpenseDate);
        if (oldPaidDate.HasValue && newPaidDate.HasValue)
            await _periodGuard.EnsureCanMutateAsync(ownerId, businessId, oldPaidDate.Value, newPaidDate.Value);
        else if (oldPaidDate.HasValue)
            await _periodGuard.EnsureCanDeleteAsync(ownerId, businessId, oldPaidDate.Value);
        else if (newPaidDate.HasValue)
            await _periodGuard.EnsureCanCreateAsync(ownerId, businessId, newPaidDate.Value);
    }

    private static void EnsureManualExpense(Expense expense)
    {
        if (expense.VoucherNumber?.StartsWith("PNK-", StringComparison.OrdinalIgnoreCase) == true)
            throw new BadRequestException("Inventory purchase expenses must be edited or deleted from inventory purchases.");
    }

    private sealed record ResolvedPayment(string? PaymentMethod, Guid? PaymentAccountId);
}
