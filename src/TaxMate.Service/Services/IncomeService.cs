using AutoMapper;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.Income;
using TaxMate.Model.DTO.MoneyMovement;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class IncomeService : IIncomeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIncomeRepository _incomes;
    private readonly IIncomeCategoryRepository _incomeCategories;
    private readonly IGenericRepository<BusinessProfile> _businessProfiles;
    private readonly IPaymentAccountService _paymentAccounts;
    private readonly IMoneyMovementService _moneyMovements;
    private readonly ITaxPeriodMutationGuard _periodGuard;
    private readonly IRevenueThresholdAlertService _revenueThresholds;
    private readonly IMapper _mapper;

    public IncomeService(
        IUnitOfWork unitOfWork,
        IIncomeRepository incomes,
        IIncomeCategoryRepository incomeCategories,
        IGenericRepository<BusinessProfile> businessProfiles,
        IPaymentAccountService paymentAccounts,
        IMoneyMovementService moneyMovements,
        ITaxPeriodMutationGuard periodGuard,
        IRevenueThresholdAlertService revenueThresholds,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _incomes = incomes;
        _incomeCategories = incomeCategories;
        _businessProfiles = businessProfiles;
        _paymentAccounts = paymentAccounts;
        _moneyMovements = moneyMovements;
        _periodGuard = periodGuard;
        _revenueThresholds = revenueThresholds;
        _mapper = mapper;
    }

    public async Task<IncomeDTO> CreateAsync(Guid ownerId, Guid businessId, CreateIncomeRequest request)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await EnsureBusinessOwnerAsync(businessId, ownerId);
            await EnsureCategoryIsValidAsync(request.IncomeCategoryId, businessId);
            var accountingType = ValidateAccountingType(request.AccountingType);
            await GuardCreateDatesAsync(ownerId, businessId, request.IncomeDate, request.ReceivedDate);
            var payment = await ResolvePaymentAsync(ownerId, businessId, request.ReceivedDate, request.PaymentMethod, request.PaymentAccountId);

            var entity = new Income
            {
                IncomeId = Guid.NewGuid(),
                BusinessId = businessId,
                IncomeCategoryId = request.IncomeCategoryId,
                IncomeTitle = request.IncomeTitle.Trim(),
                Amount = request.Amount,
                IncomeDate = request.IncomeDate,
                AccountingType = accountingType,
                PaymentMethod = payment.PaymentMethod,
                ReceiptImageUrl = request.ReceiptImageUrl,
                Note = request.Note,
                FileUrl = request.FileUrl,
                DueDate = request.DueDate,
                ReceivedDate = request.ReceivedDate
            };

            await _incomes.AddAsync(entity);
            await SyncMovementAsync(ownerId, entity, payment);
            await _unitOfWork.SaveChangesAsync();
            var created = await _incomes.GetByIdWithCategoryAsync(entity.IncomeId);
            await _unitOfWork.CommitTransactionAsync();
            if (accountingType == IncomeAccountingTypes.BusinessRevenue)
                await TryEvaluateThresholdsAsync(
                    ownerId, businessId, [RevenueYear(request.IncomeDate)]);
            return _mapper.Map<IncomeDTO>(created!);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<IncomeDTO> UpdateAsync(Guid ownerId, Guid id, UpdateIncomeRequest request)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var entity = await _incomes.GetByIdWithCategoryAsync(id);
            if (entity is null)
                throw new NotFoundException("Income not found.");
            EnsureManualIncome(entity);

            var oldAccountingType = entity.AccountingType;
            var oldIncomeDate = entity.IncomeDate;

            await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);
            await EnsureCategoryIsValidAsync(request.IncomeCategoryId, entity.BusinessId);
            var accountingType = ValidateAccountingType(request.AccountingType);
            await GuardUpdateDatesAsync(ownerId, entity.BusinessId, entity.IncomeDate, request.IncomeDate, entity.ReceivedDate, request.ReceivedDate);
            var payment = await ResolvePaymentAsync(ownerId, entity.BusinessId, request.ReceivedDate, request.PaymentMethod, request.PaymentAccountId);

            entity.IncomeCategoryId = request.IncomeCategoryId;
            entity.IncomeTitle = request.IncomeTitle.Trim();
            entity.Amount = request.Amount;
            entity.IncomeDate = request.IncomeDate;
            entity.AccountingType = accountingType;
            entity.PaymentMethod = payment.PaymentMethod;
            entity.ReceiptImageUrl = request.ReceiptImageUrl;
            entity.Note = request.Note;
            entity.FileUrl = request.FileUrl;
            entity.DueDate = request.DueDate;
            entity.ReceivedDate = request.ReceivedDate;

            _incomes.Update(entity);
            await SyncMovementAsync(ownerId, entity, payment);
            await _unitOfWork.SaveChangesAsync();
            var updated = await _incomes.GetByIdWithCategoryAsync(id);
            await _unitOfWork.CommitTransactionAsync();
            var years = new HashSet<int>();
            if (oldAccountingType == IncomeAccountingTypes.BusinessRevenue)
                years.Add(RevenueYear(oldIncomeDate));
            if (accountingType == IncomeAccountingTypes.BusinessRevenue)
                years.Add(RevenueYear(request.IncomeDate));
            await TryEvaluateThresholdsAsync(ownerId, entity.BusinessId, years);
            return _mapper.Map<IncomeDTO>(updated!);
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
            var entity = await _incomes.GetByIdAsync(id);
            if (entity is null)
                throw new NotFoundException("Income not found.");
            EnsureManualIncome(entity);

            var accountingType = entity.AccountingType;
            var incomeDate = entity.IncomeDate;

            await EnsureBusinessOwnerAsync(entity.BusinessId, ownerId);
            await _periodGuard.EnsureCanDeleteAsync(ownerId, entity.BusinessId, entity.IncomeDate);
            if (entity.ReceivedDate.HasValue && entity.ReceivedDate.Value != entity.IncomeDate)
                await _periodGuard.EnsureCanDeleteAsync(ownerId, entity.BusinessId, entity.ReceivedDate.Value);

            await _moneyMovements.DeleteAsync(ownerId, entity.BusinessId, MoneyMovementTypes.ManualIncomeIn, entity.IncomeId);
            _incomes.Remove(entity);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
            if (accountingType == IncomeAccountingTypes.BusinessRevenue)
                await TryEvaluateThresholdsAsync(
                    ownerId, entity.BusinessId, [RevenueYear(incomeDate)]);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
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

    private async Task<ResolvedPayment> ResolvePaymentAsync(Guid ownerId, Guid businessId, DateTime? receivedDate, string? paymentMethod, Guid? paymentAccountId)
    {
        if (!receivedDate.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(paymentMethod) || paymentAccountId.HasValue)
                throw new BadRequestException("Payment information is only allowed after income is received.");
            return new ResolvedPayment(null, null);
        }

        if (string.Equals(paymentMethod?.Trim(), PaymentMethods.Cash, StringComparison.OrdinalIgnoreCase))
        {
            if (paymentAccountId.HasValue)
                throw new BadRequestException("Cash income uses the system cash account automatically.");
            var cash = await _paymentAccounts.GetCashByBusinessIdAsync(ownerId, businessId);
            return new ResolvedPayment(PaymentMethods.Cash, cash.PaymentAccountId);
        }

        if (string.Equals(paymentMethod?.Trim(), PaymentMethods.Transfer, StringComparison.OrdinalIgnoreCase))
        {
            if (!paymentAccountId.HasValue)
                throw new BadRequestException("A bank account is required for transfer income.");
            return new ResolvedPayment(PaymentMethods.Transfer, paymentAccountId);
        }

        throw new BadRequestException("Payment method must be Cash or Transfer.");
    }

    private async Task SyncMovementAsync(Guid ownerId, Income income, ResolvedPayment payment)
    {
        if (!income.ReceivedDate.HasValue)
        {
            await _moneyMovements.DeleteAsync(ownerId, income.BusinessId, MoneyMovementTypes.ManualIncomeIn, income.IncomeId);
            return;
        }

        await _moneyMovements.SyncAsync(new MoneyMovementWriteRequest
        {
            OwnerId = ownerId,
            BusinessId = income.BusinessId,
            PaymentAccountId = payment.PaymentAccountId!.Value,
            PaymentMethod = payment.PaymentMethod!,
            MovementType = MoneyMovementTypes.ManualIncomeIn,
            Amount = income.Amount,
            MovementDate = income.ReceivedDate.Value,
            DocumentNumber = AccountingDocumentNumber.FromSource("PT", income.IncomeId),
            Description = $"Thu khác: {income.IncomeTitle}",
            ReferenceId = income.IncomeId
        });
    }

    private async Task GuardCreateDatesAsync(Guid ownerId, Guid businessId, DateTime incomeDate, DateTime? receivedDate)
    {
        await _periodGuard.EnsureCanCreateAsync(ownerId, businessId, incomeDate);
        if (receivedDate.HasValue && receivedDate.Value != incomeDate)
            await _periodGuard.EnsureCanCreateAsync(ownerId, businessId, receivedDate.Value);
    }

    private async Task GuardUpdateDatesAsync(Guid ownerId, Guid businessId, DateTime oldIncomeDate, DateTime newIncomeDate, DateTime? oldReceivedDate, DateTime? newReceivedDate)
    {
        await _periodGuard.EnsureCanMutateAsync(ownerId, businessId, oldIncomeDate, newIncomeDate);
        if (oldReceivedDate.HasValue && newReceivedDate.HasValue)
            await _periodGuard.EnsureCanMutateAsync(ownerId, businessId, oldReceivedDate.Value, newReceivedDate.Value);
        else if (oldReceivedDate.HasValue)
            await _periodGuard.EnsureCanDeleteAsync(ownerId, businessId, oldReceivedDate.Value);
        else if (newReceivedDate.HasValue)
            await _periodGuard.EnsureCanCreateAsync(ownerId, businessId, newReceivedDate.Value);
    }

    private static void EnsureManualIncome(Income income)
    {
        if (income.TransactionId.HasValue)
            throw new BadRequestException("Order income cannot be edited or deleted here.");
    }

    private static string ValidateAccountingType(string accountingType)
    {
        var normalized = accountingType.Trim();
        if (!IncomeAccountingTypes.All.Contains(normalized))
            throw new BadRequestException("Accounting type must be BusinessRevenue or NonRevenueCashIn.");

        return normalized;
    }

    private async Task TryEvaluateThresholdsAsync(
        Guid ownerId,
        Guid businessId,
        IEnumerable<int> years)
    {
        foreach (var year in years.Distinct())
        {
            try
            {
                await _revenueThresholds.EvaluateAsync(
                    ownerId, businessId, year);
            }
            catch
            {
                // Reconciled again when tax profile/dashboard is loaded.
            }
        }
    }

    private static int RevenueYear(DateTime value) =>
        BangkokBusinessTime.GetBangkokCalendarYear(
            BangkokBusinessTime.NormalizeNaiveUtc(value));

    private sealed record ResolvedPayment(string? PaymentMethod, Guid? PaymentAccountId);
}
