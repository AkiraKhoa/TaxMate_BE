using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class PaymentAccountService : IPaymentAccountService
{
    private readonly IUnitOfWork _unitOfWork;

    public PaymentAccountService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> CreateAsync(Guid businessId, CreatePaymentAccountRequest request)
    {
        var business = await _unitOfWork.BusinessProfiles.GetByIdAsync(businessId);
        if (business == null)
        {
            throw new Exception("Business profile not found.");
        }

        var count = await _unitOfWork.PaymentAccounts.CountAsync(x => x.BusinessId == businessId);
        var isDefault = request.IsDefault || count == 0;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            if (isDefault)
            {
                await _unitOfWork.PaymentAccounts.UnsetAllDefaultAsync(businessId);
            }

            var account = new PaymentAccount
            {
                PaymentAccountId = Guid.NewGuid(),
                BusinessId = businessId,
                BankShortName = request.BankShortName,
                BankName = request.BankName,
                AccountNumber = request.AccountNumber,
                AccountName = request.AccountName.ToUpper(),
                IsDefault = isDefault,
                Description = request.Description,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.PaymentAccounts.AddAsync(account);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            return account.PaymentAccountId;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<IEnumerable<PaymentAccountResponse>> GetByBusinessIdAsync(Guid businessId)
    {
        var accounts = await _unitOfWork.PaymentAccounts.GetAllByBusinessIdAsync(businessId);
        return accounts.Select(x => new PaymentAccountResponse
        {
            PaymentAccountId = x.PaymentAccountId,
            BusinessId = x.BusinessId,
            BankShortName = x.BankShortName,
            BankName = x.BankName,
            AccountNumber = x.AccountNumber,
            AccountName = x.AccountName,
            IsDefault = x.IsDefault,
            Description = x.Description,
            CreatedAt = x.CreatedAt
        });
    }

    public async Task<PaymentAccountResponse> GetByIdAsync(Guid id)
    {
        var x = await _unitOfWork.PaymentAccounts.GetByIdAsync(id);
        if (x == null)
        {
            throw new Exception("Payment account not found.");
        }

        return new PaymentAccountResponse
        {
            PaymentAccountId = x.PaymentAccountId,
            BusinessId = x.BusinessId,
            BankShortName = x.BankShortName,
            BankName = x.BankName,
            AccountNumber = x.AccountNumber,
            AccountName = x.AccountName,
            IsDefault = x.IsDefault,
            Description = x.Description,
            CreatedAt = x.CreatedAt
        };
    }

    public async Task UpdateAsync(Guid id, UpdatePaymentAccountRequest request)
    {
        var account = await _unitOfWork.PaymentAccounts.GetByIdAsync(id);
        if (account == null)
        {
            throw new Exception("Payment account not found.");
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            if (request.IsDefault && !account.IsDefault)
            {
                await _unitOfWork.PaymentAccounts.UnsetAllDefaultAsync(account.BusinessId);
            }

            account.BankShortName = request.BankShortName;
            account.BankName = request.BankName;
            account.AccountNumber = request.AccountNumber;
            account.AccountName = request.AccountName.ToUpper();
            account.Description = request.Description;

            if (request.IsDefault)
            {
                account.IsDefault = true;
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        var account = await _unitOfWork.PaymentAccounts.GetByIdAsync(id);
        if (account == null)
        {
            throw new Exception("Payment account not found.");
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var businessId = account.BusinessId;
            var wasDefault = account.IsDefault;

            _unitOfWork.PaymentAccounts.Remove(account);
            await _unitOfWork.SaveChangesAsync();

            if (wasDefault)
            {
                var remaining = await _unitOfWork.PaymentAccounts.GetAllByBusinessIdAsync(businessId);
                var first = remaining.FirstOrDefault();
                if (first != null)
                {
                    first.IsDefault = true;
                    await _unitOfWork.SaveChangesAsync();
                }
            }

            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task SetDefaultAsync(Guid businessId, Guid paymentAccountId)
    {
        var account = await _unitOfWork.PaymentAccounts.GetByIdAsync(paymentAccountId);
        if (account == null || account.BusinessId != businessId)
        {
            throw new Exception("Payment account not found.");
        }

        if (account.IsDefault) return;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _unitOfWork.PaymentAccounts.UnsetAllDefaultAsync(businessId);
            account.IsDefault = true;
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }
}
