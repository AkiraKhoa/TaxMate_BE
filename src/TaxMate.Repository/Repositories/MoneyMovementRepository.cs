using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;
using TaxMate.Model.Data;
using TaxMate.Model.DTO.MoneyMovement;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public sealed class MoneyMovementRepository : IMoneyMovementRepository
{
    private readonly AppDbContext _context;

    public MoneyMovementRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Guid?> GetBusinessOwnerIdAsync(
        Guid businessId,
        CancellationToken cancellationToken = default)
        => _context.BusinessProfiles
            .AsNoTracking()
            .Where(x => x.Id == businessId)
            .Select(x => (Guid?)x.OwnerId)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<PaymentAccount?> GetAccountForWriteAsync(
        Guid paymentAccountId,
        CancellationToken cancellationToken = default)
        => _context.PaymentAccounts
            .Include(x => x.Business)
            .SingleOrDefaultAsync(
                x => x.PaymentAccountId == paymentAccountId,
                cancellationToken);

    public Task<MoneyMovement?> GetBySourceForWriteAsync(
        string movementType,
        Guid referenceId,
        CancellationToken cancellationToken = default)
        => _context.MoneyMovements
            .Include(x => x.PaymentAccount)
            .ThenInclude(x => x.Business)
            .SingleOrDefaultAsync(
                x => x.MovementType == movementType && x.ReferenceId == referenceId,
                cancellationToken);

    public Task AddAsync(
        MoneyMovement movement,
        CancellationToken cancellationToken = default)
        => _context.MoneyMovements.AddAsync(movement, cancellationToken).AsTask();

    public void Remove(MoneyMovement movement)
        => _context.MoneyMovements.Remove(movement);

    public async Task<IReadOnlyList<PaymentAccount>> GetAccountsForBusinessAsync(
        Guid businessId,
        CancellationToken cancellationToken = default)
        => await _context.PaymentAccounts
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .OrderBy(x => x.AccountType)
            .ThenBy(x => x.BankName)
            .ThenBy(x => x.AccountNumber)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MoneyMovement>> GetMovementsForBusinessBeforeAsync(
        Guid businessId,
        DateTime toExclusive,
        CancellationToken cancellationToken = default)
        => await _context.MoneyMovements
            .AsNoTracking()
            .Where(x =>
                x.PaymentAccount.BusinessId == businessId &&
                x.MovementDate < toExclusive)
            .OrderBy(x => x.MovementDate)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.MoneyMovementId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MoneyMovementSourceAuditRecord>> GetExpectedSourcesBeforeAsync(
        Guid businessId,
        DateTime toExclusive,
        CancellationToken cancellationToken = default)
    {
        var payments = await _context.Payments
            .AsNoTracking()
            .Where(x =>
                x.Transaction.BusinessId == businessId &&
                x.PaidAt != null &&
                x.PaidAt < toExclusive)
            .Select(x => new MoneyMovementSourceAuditRecord
            {
                MovementType = MoneyMovementTypes.PaymentIn,
                ReferenceId = x.PaymentId,
                Amount = x.Amount,
                MovementDate = x.PaidAt!.Value,
                PaymentAccountId = x.PaymentAccountId
            })
            .ToListAsync(cancellationToken);

        var incomes = await _context.Incomes
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.TransactionId == null &&
                x.ReceivedDate != null &&
                x.ReceivedDate < toExclusive)
            .Select(x => new MoneyMovementSourceAuditRecord
            {
                MovementType = MoneyMovementTypes.ManualIncomeIn,
                ReferenceId = x.IncomeId,
                Amount = x.Amount,
                MovementDate = x.ReceivedDate!.Value
            })
            .ToListAsync(cancellationToken);

        var expenses = await _context.Expenses
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.PaidDate != null &&
                x.PaidDate < toExclusive)
            .Select(x => new MoneyMovementSourceAuditRecord
            {
                MovementType = MoneyMovementTypes.ExpenseOut,
                ReferenceId = x.ExpenseId,
                Amount = x.Amount,
                MovementDate = x.PaidDate!.Value
            })
            .ToListAsync(cancellationToken);

        return payments.Concat(incomes).Concat(expenses).ToList();
    }

    public async Task<IReadOnlySet<Guid>> GetSystemIncomeIdsBeforeAsync(
        Guid businessId,
        DateTime toExclusive,
        CancellationToken cancellationToken = default)
        => (await _context.Incomes
                .AsNoTracking()
                .Where(x =>
                    x.BusinessId == businessId &&
                    x.TransactionId != null &&
                    x.IncomeDate < toExclusive)
                .Select(x => x.IncomeId)
                .ToListAsync(cancellationToken))
            .ToHashSet();
}
