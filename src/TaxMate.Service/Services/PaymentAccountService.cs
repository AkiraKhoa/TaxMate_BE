using Microsoft.Extensions.Logging;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
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
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
    private readonly ILogger<PaymentAccountService> _logger;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, System.Threading.SemaphoreSlim> _locks = new();

    public PaymentAccountService(
        IUnitOfWork unitOfWork,
        IPaymentAccountRepository paymentAccounts,
        IGenericRepository<BusinessProfile> businessProfiles,
        ISePayService sePayService,
        ITransactionRepository transactions,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        ILogger<PaymentAccountService> logger)
    {
        _unitOfWork = unitOfWork;
        _paymentAccounts = paymentAccounts;
        _businessProfiles = businessProfiles;
        _sePayService = sePayService;
        _transactions = transactions;
        _configuration = configuration;
        _logger = logger;
    }




    public async Task<Guid> CreateAsync(Guid businessId, CreatePaymentAccountRequest request)
    {
        var business = await _businessProfiles.GetByIdAsync(businessId);
        if (business == null)
        {
            throw new NotFoundException("Business profile not found.");
        }

        var count = await _paymentAccounts.CountAsync(x => x.BusinessId == businessId);
        var isDefault = request.IsDefault || count == 0;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            if (isDefault)
            {
                await _paymentAccounts.UnsetAllDefaultAsync(businessId);
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

    public async Task<IEnumerable<PaymentAccountResponse>> GetByBusinessIdAsync(Guid businessId)
    {
        var accounts = await _paymentAccounts.GetAllByBusinessIdAsync(businessId);
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
            CassoConnectedAccountId = x.CassoConnectedAccountId,
            SePayBankAccountXid = x.SePayBankAccountXid,
            CreatedAt = x.CreatedAt
        });

    }

    public async Task<PaymentAccountResponse> GetByIdAsync(Guid id)
    {
        var x = await _paymentAccounts.GetByIdAsync(id);
        if (x == null)
        {
            throw new NotFoundException("Payment account not found.");
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
            CassoConnectedAccountId = x.CassoConnectedAccountId,
            SePayBankAccountXid = x.SePayBankAccountXid,
            CreatedAt = x.CreatedAt
        };
    }

    public async Task UpdateAsync(Guid id, UpdatePaymentAccountRequest request)
    {
        var account = await _paymentAccounts.GetByIdAsync(id);
        if (account == null)
        {
            throw new NotFoundException("Payment account not found.");
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            if (request.IsDefault && !account.IsDefault)
            {
                await _paymentAccounts.UnsetAllDefaultAsync(account.BusinessId);
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
        var account = await _paymentAccounts.GetByIdAsync(id);
        if (account == null)
        {
            // Tài khoản đã bị xóa trước bởi luồng song song (ví dụ Webhook).
            // Trả về thành công để đảm bảo tính an toàn/idempotent.
            return;
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var businessId = account.BusinessId;
            var wasDefault = account.IsDefault;

            _paymentAccounts.Remove(account);
            await _unitOfWork.SaveChangesAsync();

            if (wasDefault)
            {
                var remaining = await _paymentAccounts.GetAllByBusinessIdAsync(businessId);
                var first = remaining.FirstOrDefault();
                if (first != null)
                {
                    first.IsDefault = true;
                    await _unitOfWork.SaveChangesAsync();
                }
            }

            await _unitOfWork.CommitTransactionAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            // Tranh chấp đồng thời: tài khoản đã bị xóa trước đó.
            // Bỏ qua lỗi vì mục tiêu cuối cùng (xóa tài khoản) đã đạt được.
            await _unitOfWork.RollbackTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }


    public async Task SetDefaultAsync(Guid businessId, Guid paymentAccountId)
    {
        var account = await _paymentAccounts.GetByIdAsync(paymentAccountId);
        if (account == null || account.BusinessId != businessId)
        {
            throw new NotFoundException("Payment account not found.");
        }

        if (account.IsDefault) return;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _paymentAccounts.UnsetAllDefaultAsync(businessId);
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

    public async Task CreateOrUpdateFromCassoAsync(Guid businessId, CassoAccountDto cassoAccount, CassoTokenResponse tokens)
    {
        var existingAccount = await _paymentAccounts.FirstOrDefaultAsync(
            x => x.BusinessId == businessId && x.AccountNumber == cassoAccount.BankAccountNumber);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            if (existingAccount != null)
            {
                existingAccount.BankShortName = cassoAccount.BankName;
                existingAccount.AccountName = cassoAccount.BankAccountName.ToUpper();
                existingAccount.CassoAccessToken = tokens.AccessToken;
                existingAccount.CassoRefreshToken = tokens.RefreshToken;
                existingAccount.CassoConnectedAccountId = cassoAccount.Id.ToString();
            }
            else
            {
                var count = await _paymentAccounts.CountAsync(x => x.BusinessId == businessId);
                var isDefault = count == 0;

                if (isDefault)
                {
                    await _paymentAccounts.UnsetAllDefaultAsync(businessId);
                }

                var newAccount = new PaymentAccount
                {
                    PaymentAccountId = Guid.NewGuid(),
                    BusinessId = businessId,
                    BankShortName = cassoAccount.BankName,
                    BankName = cassoAccount.BankName,
                    AccountNumber = cassoAccount.BankAccountNumber,
                    AccountName = cassoAccount.BankAccountName.ToUpper(),
                    IsDefault = isDefault,
                    CassoAccessToken = tokens.AccessToken,
                    CassoRefreshToken = tokens.RefreshToken,
                    CassoConnectedAccountId = cassoAccount.Id.ToString(),
                    CreatedAt = DateTime.UtcNow
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

    public async Task CreateOrUpdateFromSePayAsync(string companyXid, string bankAccountXid, string bankName, string bankCode, string accountNumber, string accountName)
    {
        var business = await _businessProfiles.FirstOrDefaultAsync(x => x.SePayCompanyXid == companyXid);
        if (business == null)
        {
            throw new Exception($"Business profile not found for SePay Company XID: {companyXid}");
        }

        var sem = _locks.GetOrAdd(business.Id, _ => new System.Threading.SemaphoreSlim(1, 1));
        await sem.WaitAsync();

        try
        {
            var existingAccount = await _paymentAccounts.FirstOrDefaultAsync(
                x => x.BusinessId == business.Id && x.AccountNumber == accountNumber);

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                if (existingAccount != null)
                {
                    existingAccount.BankShortName = bankCode;
                    existingAccount.BankName = bankName;
                    existingAccount.AccountName = accountName.ToUpper();
                    existingAccount.SePayBankAccountXid = bankAccountXid;
                }
                else
                {
                    var count = await _paymentAccounts.CountAsync(x => x.BusinessId == business.Id);
                    var isDefault = count == 0;

                    if (isDefault)
                    {
                        await _paymentAccounts.UnsetAllDefaultAsync(business.Id);
                    }

                    var newAccount = new PaymentAccount
                    {
                        PaymentAccountId = Guid.NewGuid(),
                        BusinessId = business.Id,
                        BankShortName = bankCode,
                        BankName = bankName,
                        AccountNumber = accountNumber,
                        AccountName = accountName.ToUpper(),
                        IsDefault = isDefault,
                        SePayBankAccountXid = bankAccountXid,
                        CreatedAt = DateTime.UtcNow
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


    /// <summary>
    /// X\u1eed l\u00fd s\u1ef1 ki\u1ec7n BANK_ACCOUNT_LINKED t\u1eeb Bank Hub webhook.
    /// Payload kh\u00f4ng c\u00f3 company_xid tr\u1ef1c ti\u1ebfp \u2014 d\u00f9ng linkTokenXid \u0111\u1ec3 tra business.
    /// </summary>
    public async Task CreateOrUpdateFromLinkTokenAsync(
        string linkTokenXid,
        string bankAccountXid,
        string bankName,
        string accountNumber,
        string accountName)
    {
        if (string.IsNullOrEmpty(linkTokenXid))
            throw new ArgumentException("linkTokenXid is required.");

        // Tra business theo LinkTokenXid \u0111\u01b0\u1ee3c l\u01b0u l\u00fac t\u1ea1o link token
        var business = await _businessProfiles.FirstOrDefaultAsync(x => x.LastSePayLinkTokenXid == linkTokenXid);
        if (business == null)
        {
            throw new Exception($"Business profile not found for LinkToken XID: {linkTokenXid}");
        }

        // Bank Hub kh\u00f4ng tr\u1ea3 v\u1ec1 bankCode ri\u00eang \u2014 d\u00f9ng bankName l\u00e0m bankShortName t\u1ea1m
        await CreateOrUpdateFromSePayAsync(
            business.SePayCompanyXid ?? "",
            bankAccountXid,
            bankName,
            bankName, // bankCode = bankName t\u1ea1m th\u1eddi (Bank Hub ch\u1ec9 tr\u1ea3 brand_name)
            accountNumber,
            accountName
        );
    }

    /// <summary>
    /// Đồng bộ tài khoản ngân hàng từ SePay Bank Hub về DB.
    /// </summary>
    public async Task<(int Synced, int Total)> SyncSePayAccountsAsync(Guid businessId)
    {
        var business = await _businessProfiles.GetByIdAsync(businessId);
        if (business == null)
            throw new NotFoundException("Business profile not found.");

        if (string.IsNullOrEmpty(business.SePayCompanyXid))
            throw new ArgumentException("Business has no SePay company linked yet.");

        // Tự động quét và dọn dẹp các dòng trùng lặp AccountNumber của business này trong database (nếu có)
        var allAccounts = await _paymentAccounts.GetAllByBusinessIdAsync(businessId);
        var duplicates = allAccounts

            .GroupBy(x => x.AccountNumber)
            .Where(g => g.Count() > 1)
            .ToList();

        if (duplicates.Any())
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                foreach (var group in duplicates)
                {
                    // Giữ lại account đầu tiên, xóa các account trùng lặp còn lại
                    var accountsToRemove = group.Skip(1).ToList();
                    foreach (var acc in accountsToRemove)
                    {
                        var entity = await _paymentAccounts.GetByIdAsync(acc.PaymentAccountId);
                        if (entity != null)
                        {
                            _paymentAccounts.Remove(entity);
                        }
                    }
                }
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();
                _logger.LogInformation("[SePay Sync] Cleaned up duplicate bank accounts for business {BusinessId}", businessId);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogWarning(ex, "[SePay Sync] Failed to cleanup duplicate accounts for business {BusinessId}", businessId);
            }
        }

        // Chủ động gọi SePay API để lấy danh sách tài khoản đã liên kết
        var sePayAccounts = await _sePayService.GetLinkedBankAccountsAsync(business.SePayCompanyXid);

        int savedCount = 0;
        foreach (var acc in sePayAccounts)
        {
            try
            {
                await CreateOrUpdateFromSePayAsync(
                    companyXid: business.SePayCompanyXid,
                    bankAccountXid: acc.Xid,
                    bankName: acc.BrandName,
                    bankCode: acc.BrandName,
                    accountNumber: acc.AccountNumber,
                    accountName: acc.AccountHolderName
                );
                savedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SePay Sync] Failed to save account {AccountNumber}", acc.AccountNumber);
            }
        }

        return (savedCount, sePayAccounts.Count);
    }


    /// <summary>
    /// Giả lập một giao dịch chuyển tiền vào ngân hàng để kích hoạt webhook đối soát (Demo Sandbox).
    /// </summary>
    public async Task CreateMockPaymentAsync(Guid transactionId, Guid paymentAccountId)
    {
        // 1. Tìm Transaction
        var transaction = await _transactions.GetByIdAsync(transactionId);
        if (transaction == null)
            throw new NotFoundException("Transaction not found.");

        if (transaction.Status != TransactionStatus.AwaitingPayment)
            throw new InvalidOperationException("Transaction is not awaiting payment.");

        // 2. Tìm PaymentAccount
        var paymentAccount = await _paymentAccounts.GetByIdAsync(paymentAccountId);
        if (paymentAccount == null)
            throw new NotFoundException("Payment account not found.");

        if (string.IsNullOrEmpty(paymentAccount.SePayBankAccountXid))
            throw new ArgumentException("Payment account has no SePayBankAccountXid associated.");

        // 3. Gọi SePay Sandbox giả lập giao dịch chuyển khoản
        // Nội dung giao dịch chính là TransactionCode để IPN Webhook đối soát thành công
        await _sePayService.CreateMockTransactionAsync(
            paymentAccount.SePayBankAccountXid,
            transaction.TotalAmount,
            transaction.TransactionCode
        );

        _logger.LogInformation("[SePay Sandbox Mock] Triggered mock transaction. Amount={Amount}, Code={Code}, BankAccountXid={BankXid}",
            transaction.TotalAmount, transaction.TransactionCode, paymentAccount.SePayBankAccountXid);
    }

    /// <summary>
    /// Tạo hosted link hủy kết nối SePay Bank Hub, lưu linkTokenXid vào DB và đăng ký webhook.
    /// </summary>
    public async Task<string> GetSePayDisconnectUrlAsync(Guid paymentAccountId, string scheme, string host)
    {
        var account = await _paymentAccounts.GetByIdAsync(paymentAccountId);
        if (account == null)
            throw new NotFoundException("Payment account not found.");

        if (string.IsNullOrEmpty(account.SePayBankAccountXid))
            throw new ArgumentException("Payment account is not connected via SePay.");

        var business = await _businessProfiles.GetByIdAsync(account.BusinessId);
        if (business == null)
            throw new NotFoundException("Business profile not found.");

        var companyXid = business.SePayCompanyXid;
        if (string.IsNullOrEmpty(companyXid))
            throw new ArgumentException("Business has no SePay company linked.");

        var redirectUri = $"{scheme}://{host}/api/PaymentAccount/sepay-callback";
        if (host.Contains("localhost") || host.Contains("127.0.0.1"))
        {
            redirectUri = "https://taxmate.vn/api/PaymentAccount/sepay-callback";
        }

        // Tạo link token hủy liên kết và lấy cả URL lẫn linkTokenXid
        var (url, linkTokenXid) = await _sePayService.GenerateHostedLinkUrlAsync(
            companyXid, redirectUri, "UNLINK_BANK_ACCOUNT", account.SePayBankAccountXid);

        // Lưu linkTokenXid vào BusinessProfile để sau này trace BANK_ACCOUNT_UNLINKED webhook
        if (!string.IsNullOrEmpty(linkTokenXid))
        {
            business.LastSePayLinkTokenXid = linkTokenXid;
            _businessProfiles.Update(business);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("[SePay Unlink] Saved LinkTokenXid={Xid} for BusinessId={BusinessId}", linkTokenXid, business.Id);
        }

        // Đăng ký Webhook URL với SePay Bank Hub
        var webhookBaseUrl = _configuration["SePay:BankHub:WebhookUrl"];
        if (string.IsNullOrEmpty(webhookBaseUrl))
        {
            webhookBaseUrl = $"{scheme}://{host}";
        }
        var webhookUrl = $"{webhookBaseUrl}/api/webhook/payment/bankhub";
        var secretKey = _configuration["SePay:BankHub:SecretKey"] ?? "";


        _ = _sePayService.RegisterWebhookAsync(webhookUrl, secretKey)
            .ContinueWith(t => _logger.LogWarning(t.Exception, "[SePay] RegisterWebhook error"),
                TaskContinuationOptions.OnlyOnFaulted);

        return url;
    }

    /// <summary>
    /// Tìm và xóa tài khoản ngân hàng cục bộ dựa trên bankAccountXid của SePay Bank Hub.
    /// Dùng khi xử lý webhook BANK_ACCOUNT_UNLINKED.
    /// </summary>
    public async Task DeleteBySePayBankAccountXidAsync(string bankAccountXid)
    {
        if (string.IsNullOrEmpty(bankAccountXid))
            return;

        var account = await _paymentAccounts.FirstOrDefaultAsync(x => x.SePayBankAccountXid == bankAccountXid);
        if (account != null)
        {
            await DeleteAsync(account.PaymentAccountId);
            _logger.LogInformation("[SePay Unlink Webhook] Deleted payment account {AccountNumber} (Xid: {Xid}) from DB.",
                account.AccountNumber, bankAccountXid);
        }
        else
        {
            _logger.LogWarning("[SePay Unlink Webhook] Webhook requested delete for account Xid {Xid} but it was not found in DB.",
                bankAccountXid);
        }
    }
}




