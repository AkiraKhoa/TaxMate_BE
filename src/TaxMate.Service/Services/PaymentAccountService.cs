using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class PaymentAccountService : IPaymentAccountService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentAccountRepository _paymentAccounts;
    private readonly IGenericRepository<BusinessProfile> _businessProfiles;
    private readonly ISePayService _sePayService;
    private readonly ITransactionRepository _transactions;
    private readonly ITaxPeriodMutationGuard _mutationGuard;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentAccountService> _logger;

    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Locks = new();

    public PaymentAccountService(
        IUnitOfWork unitOfWork,
        IPaymentAccountRepository paymentAccounts,
        IGenericRepository<BusinessProfile> businessProfiles,
        ISePayService sePayService,
        ITransactionRepository transactions,
        ITaxPeriodMutationGuard mutationGuard,
        IConfiguration configuration,
        ILogger<PaymentAccountService> logger)
    {
        _unitOfWork = unitOfWork;
        _paymentAccounts = paymentAccounts;
        _businessProfiles = businessProfiles;
        _sePayService = sePayService;
        _transactions = transactions;
        _mutationGuard = mutationGuard;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Guid> CreateAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        CreatePaymentAccountRequest request)
    {
        await EnsureBusinessOwnerAsync(businessId, authenticatedOwnerId);
        ValidateBankFields(
            request.BankShortName,
            request.BankName,
            request.AccountNumber,
            request.AccountName);
        ValidateInitialBalancePair(
            request.InitialBalance,
            request.InitialBalanceDate);

        var normalizedAccountNumber = NormalizeAccountNumber(request.AccountNumber);
        var sem = Locks.GetOrAdd(businessId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync();

        try
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var duplicate = await _paymentAccounts.GetBankByAccountNumberAsync(
                    businessId,
                    normalizedAccountNumber);
                if (duplicate is not null)
                {
                    throw new ConflictException(
                        "A bank account with this account number already exists.");
                }

                if (request.InitialBalanceDate.HasValue)
                {
                    await _mutationGuard.EnsureCanCreateAsync(
                        authenticatedOwnerId,
                        businessId,
                        ToAccountingInstant(request.InitialBalanceDate.Value));
                }

                var currentDefault = await _paymentAccounts.GetDefaultByBusinessIdAsync(
                    businessId);
                var isDefault = request.IsDefault || currentDefault is null;
                if (isDefault)
                {
                    await _paymentAccounts.UnsetAllDefaultAsync(businessId);
                }

                var now = DateTime.UtcNow;
                var account = new PaymentAccount
                {
                    PaymentAccountId = Guid.NewGuid(),
                    BusinessId = businessId,
                    AccountType = PaymentAccountTypes.Bank,
                    BankShortName = NormalizeBankCode(request.BankShortName),
                    BankName = NormalizeRequiredText(request.BankName),
                    AccountNumber = normalizedAccountNumber,
                    AccountName = NormalizeAccountName(request.AccountName),
                    InitialBalance = request.InitialBalance,
                    InitialBalanceDate = request.InitialBalanceDate,
                    IsActive = true,
                    IsDefault = isDefault,
                    Description = NormalizeOptionalText(request.Description),
                    CreatedAt = now,
                    UpdatedAt = now
                };

                await _paymentAccounts.AddAsync(account);
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
        finally
        {
            sem.Release();
        }
    }

    public async Task<IEnumerable<PaymentAccountResponse>> GetByBusinessIdAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        bool includeInactive = false)
    {
        await EnsureBusinessOwnerAsync(businessId, authenticatedOwnerId);
        var accounts = await _paymentAccounts.GetAllByBusinessIdAsync(
            businessId,
            includeInactive);
        return accounts
            .Where(x => x.AccountType == PaymentAccountTypes.Bank)
            .Select(MapResponse)
            .ToList();
    }

    public async Task<IEnumerable<PaymentAccountResponse>> GetAllMoneyAccountsByBusinessIdAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        bool includeInactive = false)
    {
        await EnsureBusinessOwnerAsync(businessId, authenticatedOwnerId);
        var accounts = await _paymentAccounts.GetAllByBusinessIdAsync(
            businessId,
            includeInactive);
        return accounts.Select(MapResponse).ToList();
    }

    public async Task<PaymentAccountResponse> GetCashByBusinessIdAsync(
        Guid authenticatedOwnerId,
        Guid businessId)
    {
        await EnsureBusinessOwnerAsync(businessId, authenticatedOwnerId);
        var cash = await _paymentAccounts.GetCashByBusinessIdAsync(businessId);
        if (cash is null)
        {
            throw new NotFoundException("Cash account not found.");
        }

        return MapResponse(cash);
    }

    public async Task<PaymentAccountResponse> GetByIdAsync(
        Guid authenticatedOwnerId,
        Guid id)
    {
        var account = await GetOwnedAccountAsync(authenticatedOwnerId, id);
        return MapResponse(account);
    }

    public async Task UpdateAsync(
        Guid authenticatedOwnerId,
        Guid id,
        UpdatePaymentAccountRequest request)
    {
        var account = await GetOwnedAccountAsync(authenticatedOwnerId, id);
        if (account.AccountType == PaymentAccountTypes.Cash)
        {
            throw new ConflictException(
                "The Cash account is managed by the system. Only its initial balance can be updated.");
        }

        ValidateBankFields(
            request.BankShortName,
            request.BankName,
            request.AccountNumber,
            request.AccountName);
        var normalizedAccountNumber = NormalizeAccountNumber(request.AccountNumber);
        if (!string.IsNullOrWhiteSpace(account.SePayBankAccountXid) &&
            !string.Equals(
                account.AccountNumber,
                normalizedAccountNumber,
                StringComparison.Ordinal))
        {
            throw new ConflictException(
                "A SePay-linked account number cannot be changed. Disconnect it first.");
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var duplicate = await _paymentAccounts.GetBankByAccountNumberAsync(
                account.BusinessId,
                normalizedAccountNumber);
            if (duplicate is not null && duplicate.PaymentAccountId != account.PaymentAccountId)
            {
                throw new ConflictException(
                    "A bank account with this account number already exists.");
            }

            if (request.IsDefault && !account.IsDefault)
            {
                if (!account.IsActive)
                {
                    throw new ConflictException(
                        "An inactive bank account cannot be the default account.");
                }

                await _paymentAccounts.UnsetAllDefaultAsync(account.BusinessId);
                account.IsDefault = true;
            }

            account.BankShortName = NormalizeBankCode(request.BankShortName);
            account.BankName = NormalizeRequiredText(request.BankName);
            account.AccountNumber = normalizedAccountNumber;
            account.AccountName = NormalizeAccountName(request.AccountName);
            account.Description = NormalizeOptionalText(request.Description);
            if (!account.IsActive)
            {
                account.IsDefault = false;
            }
            account.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task UpdateInitialBalanceAsync(
        Guid authenticatedOwnerId,
        Guid id,
        UpdatePaymentAccountInitialBalanceRequest request)
    {
        ValidateInitialBalancePair(
            request.InitialBalance,
            request.InitialBalanceDate);

        var account = await GetOwnedAccountAsync(authenticatedOwnerId, id);
        if (account.InitialBalance == request.InitialBalance &&
            account.InitialBalanceDate == request.InitialBalanceDate)
        {
            return;
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await EnsureInitialBalancePeriodIsOpenAsync(
                authenticatedOwnerId,
                account,
                request.InitialBalanceDate);

            account.InitialBalance = request.InitialBalance;
            account.InitialBalanceDate = request.InitialBalanceDate;
            account.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task DeactivateAsync(Guid authenticatedOwnerId, Guid id)
    {
        var account = await GetOwnedAccountAsync(authenticatedOwnerId, id);
        if (account.AccountType == PaymentAccountTypes.Cash)
        {
            throw new ConflictException("The system Cash account cannot be deactivated.");
        }

        if (!account.IsActive && !HasAnyIntegrationIdentifier(account))
        {
            return;
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var wasDefault = account.IsDefault;
            DeactivateAndClearAllIntegrations(account);
            if (wasDefault)
            {
                await PromoteNextDefaultBankAsync(
                    account.BusinessId,
                    [account.PaymentAccountId]);
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

    public async Task ActivateAsync(Guid authenticatedOwnerId, Guid id)
    {
        var account = await GetOwnedAccountAsync(authenticatedOwnerId, id);
        if (account.AccountType == PaymentAccountTypes.Cash)
        {
            throw new ConflictException("The system Cash account is always active.");
        }

        if (account.IsActive)
        {
            return;
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            account.IsActive = true;
            account.UpdatedAt = DateTime.UtcNow;
            account.IsDefault = await _paymentAccounts.GetDefaultByBusinessIdAsync(
                account.BusinessId) is null;

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task SetDefaultAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        Guid paymentAccountId)
    {
        await EnsureBusinessOwnerAsync(businessId, authenticatedOwnerId);
        var account = await _paymentAccounts.GetByIdAsync(paymentAccountId);
        if (account is null || account.BusinessId != businessId)
        {
            throw new NotFoundException("Payment account not found.");
        }

        if (account.AccountType != PaymentAccountTypes.Bank || !account.IsActive)
        {
            throw new ConflictException(
                "Only an active bank account can be the default bank account.");
        }

        if (account.IsDefault)
        {
            return;
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _paymentAccounts.UnsetAllDefaultAsync(businessId);
            account.IsDefault = true;
            account.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<Guid> EnsureCashAccountAsync(Guid businessId)
    {
        var business = await _businessProfiles.GetByIdAsync(businessId);
        if (business is null)
        {
            throw new NotFoundException("Business profile not found.");
        }

        var sem = Locks.GetOrAdd(businessId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync();
        try
        {
            var existing = await _paymentAccounts.GetCashByBusinessIdAsync(businessId);
            if (existing is not null)
            {
                if (!existing.IsActive || existing.IsDefault)
                {
                    existing.IsActive = true;
                    existing.IsDefault = false;
                    existing.UpdatedAt = DateTime.UtcNow;
                    await _unitOfWork.SaveChangesAsync();
                }

                return existing.PaymentAccountId;
            }

            var now = DateTime.UtcNow;
            var account = new PaymentAccount
            {
                PaymentAccountId = Guid.NewGuid(),
                BusinessId = businessId,
                AccountType = PaymentAccountTypes.Cash,
                IsActive = true,
                IsDefault = false,
                Description = "Tiền mặt",
                CreatedAt = now,
                UpdatedAt = now
            };

            await _paymentAccounts.AddAsync(account);
            await _unitOfWork.SaveChangesAsync();
            return account.PaymentAccountId;
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task CreateOrUpdateFromSePayAsync(
        string companyXid,
        string bankAccountXid,
        string bankName,
        string bankCode,
        string accountNumber,
        string accountName)
    {
        var business = await _businessProfiles.FirstOrDefaultAsync(
            x => x.SePayCompanyXid == companyXid);
        if (business is null)
        {
            throw new NotFoundException(
                $"Business profile not found for SePay Company XID: {companyXid}");
        }

        await UpsertSePayBankAccountAsync(
            business,
            bankAccountXid,
            bankName,
            bankCode,
            accountNumber,
            accountName);
    }

    public async Task CreateOrUpdateFromLinkTokenAsync(
        string linkTokenXid,
        string bankAccountXid,
        string bankName,
        string accountNumber,
        string accountName)
    {
        if (string.IsNullOrWhiteSpace(linkTokenXid))
        {
            throw new ArgumentException("linkTokenXid is required.");
        }

        var business = await _businessProfiles.FirstOrDefaultAsync(
            x => x.LastSePayLinkTokenXid == linkTokenXid);
        if (business is null)
        {
            throw new NotFoundException(
                $"Business profile not found for LinkToken XID: {linkTokenXid}");
        }

        await UpsertSePayBankAccountAsync(
            business,
            bankAccountXid,
            bankName,
            bankName,
            accountNumber,
            accountName);
    }

    public async Task<string> GetSePayConnectUrlAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        bool isMobileApp = true)
    {
        await EnsureBusinessOwnerAsync(businessId, authenticatedOwnerId);
        return await _sePayService.GetSePayConnectUrlAsync(
            businessId,
            isMobileApp);
    }

    public async Task<(int Synced, int Total)> SyncSePayAccountsAsync(
        Guid authenticatedOwnerId,
        Guid businessId)
    {
        var business = await EnsureBusinessOwnerAsync(
            businessId,
            authenticatedOwnerId);
        if (string.IsNullOrWhiteSpace(business.SePayCompanyXid))
        {
            throw new ArgumentException("Business has no SePay company linked yet.");
        }

        await NormalizeAndDeactivateDuplicateBanksAsync(businessId);

        var sePayAccounts = await _sePayService.GetLinkedBankAccountsAsync(
            business.SePayCompanyXid);
        var remoteXids = sePayAccounts
            .Select(x => x.Xid)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);

        await DeactivateMissingSePayAccountsAsync(businessId, remoteXids);

        var savedCount = 0;
        foreach (var account in sePayAccounts)
        {
            try
            {
                await UpsertSePayBankAccountAsync(
                    business,
                    account.Xid,
                    account.BrandName,
                    account.BrandName,
                    account.AccountNumber,
                    account.AccountHolderName);
                savedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[SePay Sync] Failed to save a bank account.");
            }
        }

        return (savedCount, sePayAccounts.Count);
    }

    public async Task CreateMockPaymentAsync(
        Guid authenticatedOwnerId,
        Guid transactionId,
        Guid paymentAccountId)
    {
        var transaction = await _transactions.GetByIdAsync(transactionId);
        if (transaction is null)
        {
            throw new NotFoundException("Transaction not found.");
        }

        await EnsureBusinessOwnerAsync(transaction.BusinessId, authenticatedOwnerId);
        if (transaction.Status != TransactionStatus.AwaitingPayment)
        {
            throw new InvalidOperationException("Transaction is not awaiting payment.");
        }

        var paymentAccount = await _paymentAccounts.GetByIdAsync(paymentAccountId);
        if (paymentAccount is null ||
            paymentAccount.BusinessId != transaction.BusinessId ||
            paymentAccount.AccountType != PaymentAccountTypes.Bank ||
            !paymentAccount.IsActive)
        {
            throw new NotFoundException("Payment account not found.");
        }

        if (string.IsNullOrWhiteSpace(paymentAccount.SePayBankAccountXid))
        {
            throw new ArgumentException(
                "Payment account has no SePayBankAccountXid associated.");
        }

        await _sePayService.CreateMockTransactionAsync(
            paymentAccount.SePayBankAccountXid,
            transaction.TotalAmount,
            transaction.TransactionCode);

        _logger.LogInformation(
            "[SePay Sandbox Mock] Triggered mock transaction. Amount={Amount}, Code={Code}",
            transaction.TotalAmount,
            transaction.TransactionCode);
    }

    public async Task<string> GetSePayDisconnectUrlAsync(
        Guid authenticatedOwnerId,
        Guid paymentAccountId)
    {
        var callbackUrl = GetRequiredHttpsUrl("SePay:BankHub:CallbackUrl");
        var webhookUrl = GetRequiredHttpsUrl("SePay:BankHub:WebhookUrl");
        var webhookSecret = GetRequiredConfiguration("SePay:BankHub:SecretKey");
        var account = await GetOwnedAccountAsync(
            authenticatedOwnerId,
            paymentAccountId);
        if (account.AccountType != PaymentAccountTypes.Bank ||
            string.IsNullOrWhiteSpace(account.SePayBankAccountXid))
        {
            throw new ArgumentException("Payment account is not connected via SePay.");
        }

        var business = await _businessProfiles.GetByIdAsync(account.BusinessId)
            ?? throw new NotFoundException("Business profile not found.");
        var sePayAccountDetail = await _sePayService.GetBankAccountDetailAsync(
            account.SePayBankAccountXid);
        var companyXid = sePayAccountDetail?.CompanyXid ?? business.SePayCompanyXid;
        if (string.IsNullOrWhiteSpace(companyXid))
        {
            throw new ArgumentException("Business has no SePay company linked.");
        }

        var (url, linkTokenXid) = await _sePayService.GenerateHostedLinkUrlAsync(
            companyXid,
            callbackUrl,
            "UNLINK_BANK_ACCOUNT",
            account.SePayBankAccountXid);

        if (!string.IsNullOrWhiteSpace(linkTokenXid))
        {
            business.LastSePayLinkTokenXid = linkTokenXid;
            _businessProfiles.Update(business);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation(
                "[SePay Unlink] Saved unlink correlation for BusinessId={BusinessId}",
                business.Id);
        }

        await _sePayService.RegisterWebhookAsync(webhookUrl, webhookSecret);
        return url;
    }

    public async Task DeleteBySePayBankAccountXidAsync(string bankAccountXid)
    {
        if (string.IsNullOrWhiteSpace(bankAccountXid))
        {
            return;
        }

        var account = await _paymentAccounts.FirstOrDefaultAsync(
            x => x.SePayBankAccountXid == bankAccountXid &&
                 x.AccountType == PaymentAccountTypes.Bank);
        if (account is null)
        {
            _logger.LogWarning(
                "[SePay Unlink Webhook] Linked bank account was not found in DB.");
            return;
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var wasDefault = account.IsDefault;
            DeactivateAndClearSePay(account);
            if (wasDefault)
            {
                await PromoteNextDefaultBankAsync(
                    account.BusinessId,
                    [account.PaymentAccountId]);
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
            _logger.LogInformation(
                "[SePay Unlink Webhook] Deactivated bank account while preserving history.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<(int Recovered, int Total)> RecoverAllFromSePayAsync(
        Guid authenticatedOwnerId)
    {
        var businesses = (await _businessProfiles.FindAsync(
                x => x.OwnerId == authenticatedOwnerId &&
                     x.SePayCompanyXid != null))
            .Where(x => !string.IsNullOrWhiteSpace(x.SePayCompanyXid))
            .ToList();
        if (businesses.Count == 0)
        {
            return (0, 0);
        }

        var recovered = 0;
        var total = 0;
        foreach (var business in businesses)
        {
            var remoteAccounts = await _sePayService.GetLinkedBankAccountsAsync(
                business.SePayCompanyXid);
            total += remoteAccounts.Count;
            foreach (var account in remoteAccounts)
            {
                try
                {
                    await UpsertSePayBankAccountAsync(
                        business,
                        account.Xid,
                        account.BrandName,
                        account.BrandName,
                        account.AccountNumber,
                        account.AccountHolderName);
                    recovered++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "[SePay Recover] Failed to recover an account for owned business {BusinessId}",
                        business.Id);
                }
            }
        }

        return (recovered, total);
    }

    private async Task UpsertSePayBankAccountAsync(
        BusinessProfile business,
        string bankAccountXid,
        string bankName,
        string bankCode,
        string accountNumber,
        string accountName)
    {
        ValidateBankFields(bankCode, bankName, accountNumber, accountName);
        var normalizedNumber = NormalizeAccountNumber(accountNumber);
        var sem = Locks.GetOrAdd(business.Id, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync();
        try
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var normalizedXid = NormalizeRequiredText(bankAccountXid);
                var existingByXid = await _paymentAccounts.GetBankBySePayXidAsync(
                    normalizedXid);
                var existingByNumber = await _paymentAccounts.GetBankByAccountNumberAsync(
                    business.Id,
                    normalizedNumber);
                PaymentAccount? existing;
                if (existingByXid is not null)
                {
                    if (existingByXid.BusinessId != business.Id ||
                        !string.Equals(
                            NormalizeAccountNumber(existingByXid.AccountNumber!),
                            normalizedNumber,
                            StringComparison.Ordinal) ||
                        (existingByNumber is not null &&
                         existingByNumber.PaymentAccountId != existingByXid.PaymentAccountId))
                    {
                        throw new ConflictException(
                            "SePay bank account identity conflicts with an existing payment account.");
                    }

                    existing = existingByXid;
                }
                else if (existingByNumber is not null)
                {
                    if (!string.IsNullOrWhiteSpace(existingByNumber.SePayBankAccountXid))
                    {
                        throw new ConflictException(
                            "This account number is already linked to a different SePay bank account.");
                    }

                    // Account-number fallback is intentionally limited to
                    // unlinked legacy rows created before SePay XID identity.
                    existing = existingByNumber;
                }
                else
                {
                    existing = null;
                }

                var currentDefault = await _paymentAccounts.GetDefaultByBusinessIdAsync(
                    business.Id);

                if (existing is not null)
                {
                    existing.AccountType = PaymentAccountTypes.Bank;
                    existing.BankShortName = NormalizeBankCode(bankCode);
                    existing.BankName = NormalizeRequiredText(bankName);
                    existing.AccountNumber = normalizedNumber;
                    existing.AccountName = NormalizeAccountName(accountName);
                    existing.SePayBankAccountXid = normalizedXid;
                    existing.IsActive = true;
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.IsDefault = currentDefault is null ||
                                         currentDefault.PaymentAccountId == existing.PaymentAccountId;
                }
                else
                {
                    var now = DateTime.UtcNow;
                    var newAccount = new PaymentAccount
                    {
                        PaymentAccountId = Guid.NewGuid(),
                        BusinessId = business.Id,
                        AccountType = PaymentAccountTypes.Bank,
                        BankShortName = NormalizeBankCode(bankCode),
                        BankName = NormalizeRequiredText(bankName),
                        AccountNumber = normalizedNumber,
                        AccountName = NormalizeAccountName(accountName),
                        IsActive = true,
                        IsDefault = currentDefault is null,
                        SePayBankAccountXid = normalizedXid,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    await _paymentAccounts.AddAsync(newAccount);
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
        finally
        {
            sem.Release();
        }
    }

    private async Task NormalizeAndDeactivateDuplicateBanksAsync(Guid businessId)
    {
        var accounts = (await _paymentAccounts.GetAllByBusinessIdAsync(
                businessId,
                includeInactive: true))
            .Where(x => x.AccountType == PaymentAccountTypes.Bank)
            .ToList();

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            foreach (var account in accounts)
            {
                account.BankShortName = NormalizeBankCode(account.BankShortName!);
                account.BankName = NormalizeRequiredText(account.BankName!);
                account.AccountNumber = NormalizeAccountNumber(account.AccountNumber!);
                account.AccountName = NormalizeAccountName(account.AccountName!);
            }

            var duplicateGroups = accounts
                .GroupBy(x => x.AccountNumber!, StringComparer.Ordinal)
                .Where(x => x.Count() > 1);

            foreach (var group in duplicateGroups)
            {
                var keep = group
                    .OrderByDescending(x => x.IsActive)
                    .ThenByDescending(x => x.IsDefault)
                    .ThenBy(x => x.CreatedAt)
                    .First();
                foreach (var duplicate in group.Where(
                             x => x.PaymentAccountId != keep.PaymentAccountId))
                {
                    DeactivateAndClearSePay(duplicate);
                }
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

    private async Task DeactivateMissingSePayAccountsAsync(
        Guid businessId,
        IReadOnlySet<string> remoteXids)
    {
        var accounts = (await _paymentAccounts.GetAllByBusinessIdAsync(
                businessId,
                includeInactive: true))
            .Where(x =>
                x.AccountType == PaymentAccountTypes.Bank &&
                !string.IsNullOrWhiteSpace(x.SePayBankAccountXid) &&
                !remoteXids.Contains(x.SePayBankAccountXid))
            .ToList();
        if (accounts.Count == 0)
        {
            return;
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var removedDefault = false;
            foreach (var account in accounts)
            {
                removedDefault |= account.IsDefault;
                DeactivateAndClearSePay(account);
            }

            if (removedDefault)
            {
                await PromoteNextDefaultBankAsync(
                    businessId,
                    accounts.Select(x => x.PaymentAccountId).ToArray());
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

    private async Task EnsureInitialBalancePeriodIsOpenAsync(
        Guid authenticatedOwnerId,
        PaymentAccount account,
        DateOnly? newDate)
    {
        if (account.InitialBalanceDate.HasValue && newDate.HasValue)
        {
            await _mutationGuard.EnsureCanMutateAsync(
                authenticatedOwnerId,
                account.BusinessId,
                ToAccountingInstant(account.InitialBalanceDate.Value),
                ToAccountingInstant(newDate.Value));
        }
        else if (account.InitialBalanceDate.HasValue)
        {
            await _mutationGuard.EnsureCanDeleteAsync(
                authenticatedOwnerId,
                account.BusinessId,
                ToAccountingInstant(account.InitialBalanceDate.Value));
        }
        else if (newDate.HasValue)
        {
            await _mutationGuard.EnsureCanCreateAsync(
                authenticatedOwnerId,
                account.BusinessId,
                ToAccountingInstant(newDate.Value));
        }
    }

    private async Task<BusinessProfile> EnsureBusinessOwnerAsync(
        Guid businessId,
        Guid authenticatedOwnerId)
    {
        var business = await _businessProfiles.GetByIdAsync(businessId);
        if (business is null || business.OwnerId != authenticatedOwnerId)
        {
            throw new NotFoundException("Business profile not found.");
        }

        return business;
    }

    private async Task<PaymentAccount> GetOwnedAccountAsync(
        Guid authenticatedOwnerId,
        Guid paymentAccountId)
    {
        var account = await _paymentAccounts.GetByIdAsync(paymentAccountId);
        if (account is null)
        {
            throw new NotFoundException("Payment account not found.");
        }

        var business = await _businessProfiles.GetByIdAsync(account.BusinessId);
        if (business is null || business.OwnerId != authenticatedOwnerId)
        {
            throw new NotFoundException("Payment account not found.");
        }

        return account;
    }

    private async Task PromoteNextDefaultBankAsync(
        Guid businessId,
        IReadOnlyCollection<Guid> excludedPaymentAccountIds)
    {
        var next = await _paymentAccounts.GetFirstActiveBankAsync(
            businessId,
            excludedPaymentAccountIds);
        if (next is not null)
        {
            next.IsDefault = true;
            next.UpdatedAt = DateTime.UtcNow;
        }
    }

    private string GetRequiredHttpsUrl(string configurationKey)
    {
        var value = GetRequiredConfiguration(configurationKey);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException(
                $"Configuration '{configurationKey}' must be an absolute HTTPS URL without credentials or fragments.");
        }

        return uri.AbsoluteUri;
    }

    private string GetRequiredConfiguration(string configurationKey)
    {
        var value = _configuration[configurationKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Required configuration '{configurationKey}' is missing.");
        }

        return value.Trim();
    }

    private static PaymentAccountResponse MapResponse(PaymentAccount account)
        => new()
        {
            PaymentAccountId = account.PaymentAccountId,
            BusinessId = account.BusinessId,
            AccountType = account.AccountType,
            BankShortName = account.BankShortName,
            BankName = account.BankName,
            AccountNumber = account.AccountNumber,
            AccountName = account.AccountName,
            InitialBalance = account.InitialBalance,
            InitialBalanceDate = account.InitialBalanceDate,
            IsActive = account.IsActive,
            IsDefault = account.IsDefault,
            Description = account.Description,
            CassoConnectedAccountId = account.CassoConnectedAccountId,
            SePayBankAccountXid = account.SePayBankAccountXid,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt
        };

    private static void ValidateBankFields(
        string? bankShortName,
        string? bankName,
        string? accountNumber,
        string? accountName)
    {
        if (string.IsNullOrWhiteSpace(bankShortName) ||
            string.IsNullOrWhiteSpace(bankName) ||
            string.IsNullOrWhiteSpace(accountNumber) ||
            string.IsNullOrWhiteSpace(accountName))
        {
            throw new BadRequestException(
                "Bank code, bank name, account number and account name are required for a Bank account.");
        }
    }

    private static void ValidateInitialBalancePair(
        decimal? initialBalance,
        DateOnly? initialBalanceDate)
    {
        if (initialBalance.HasValue != initialBalanceDate.HasValue)
        {
            throw new BadRequestException(
                "InitialBalance and InitialBalanceDate must either both be provided or both be null.");
        }
    }

    private static string NormalizeBankCode(string value)
        => NormalizeRequiredText(value).ToUpperInvariant();

    private static string NormalizeAccountName(string value)
        => NormalizeRequiredText(value).ToUpperInvariant();

    private static string NormalizeAccountNumber(string value)
        => string.Concat(NormalizeRequiredText(value).Where(x => !char.IsWhiteSpace(x)));

    private static string NormalizeRequiredText(string value)
        => value.Trim();

    private static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime ToAccountingInstant(DateOnly date)
        => BangkokBusinessTime.BangkokWallClockToNaiveUtc(
            date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified));

    private static bool HasAnyIntegrationIdentifier(PaymentAccount account)
        => !string.IsNullOrWhiteSpace(account.SePayBankAccountXid) ||
           !string.IsNullOrWhiteSpace(account.CassoConnectedAccountId) ||
           !string.IsNullOrWhiteSpace(account.CassoAccessToken) ||
           !string.IsNullOrWhiteSpace(account.CassoRefreshToken);

    private static void DeactivateAndClearSePay(PaymentAccount account)
    {
        account.IsActive = false;
        account.IsDefault = false;
        account.SePayBankAccountXid = null;
        account.UpdatedAt = DateTime.UtcNow;
    }

    private static void DeactivateAndClearAllIntegrations(PaymentAccount account)
    {
        DeactivateAndClearSePay(account);
        account.CassoAccessToken = null;
        account.CassoRefreshToken = null;
        account.CassoConnectedAccountId = null;
    }
}
