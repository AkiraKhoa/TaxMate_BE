using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class PaymentAccountRepository : GenericRepository<PaymentAccount>, IPaymentAccountRepository
{
    public PaymentAccountRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PaymentAccount?> GetDefaultByBusinessIdAsync(Guid businessId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(x =>
                x.BusinessId == businessId &&
                x.AccountType == PaymentAccountTypes.Bank &&
                x.IsActive &&
                x.IsDefault);
    }

    public Task<PaymentAccount?> GetCashByBusinessIdAsync(Guid businessId)
        => _dbSet.FirstOrDefaultAsync(x =>
            x.BusinessId == businessId &&
            x.AccountType == PaymentAccountTypes.Cash);

    public Task<PaymentAccount?> GetBankByAccountNumberAsync(
        Guid businessId,
        string accountNumber)
        => _dbSet
            .Where(x =>
                x.BusinessId == businessId &&
                x.AccountType == PaymentAccountTypes.Bank &&
                x.AccountNumber == accountNumber)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.IsDefault)
            .ThenBy(x => x.CreatedAt)
            .FirstOrDefaultAsync();

    public Task<PaymentAccount?> GetBankBySePayXidAsync(
        string sePayBankAccountXid)
        => _dbSet.SingleOrDefaultAsync(x =>
            x.AccountType == PaymentAccountTypes.Bank &&
            x.SePayBankAccountXid == sePayBankAccountXid);

    public async Task<IEnumerable<PaymentAccount>> GetAllByBusinessIdAsync(
        Guid businessId,
        bool includeInactive = false)
    {
        var query = _dbSet.Where(x => x.BusinessId == businessId);
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.AccountType == PaymentAccountTypes.Cash ? 0 : 1)
            .ThenByDescending(x => x.IsDefault)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync();
    }

    public Task<PaymentAccount?> GetFirstActiveBankAsync(
        Guid businessId,
        IReadOnlyCollection<Guid> excludedPaymentAccountIds)
        => _dbSet
            .Where(x =>
                x.BusinessId == businessId &&
                x.AccountType == PaymentAccountTypes.Bank &&
                x.IsActive &&
                !excludedPaymentAccountIds.Contains(x.PaymentAccountId))
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync();

    public Task<bool> HasMoneyMovementHistoryAsync(Guid paymentAccountId)
        => _dbSet.AnyAsync(x =>
            x.PaymentAccountId == paymentAccountId &&
            x.MoneyMovements.Any());

    public async Task UnsetAllDefaultAsync(Guid businessId)
    {
        var defaults = await _dbSet
            .Where(x =>
                x.BusinessId == businessId &&
                x.AccountType == PaymentAccountTypes.Bank &&
                x.IsDefault)
            .ToListAsync();

        foreach (var account in defaults)
        {
            account.IsDefault = false;
        }
    }
}
