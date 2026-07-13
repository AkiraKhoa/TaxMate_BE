using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class PaymentWebhookService : IPaymentWebhookService
{
    private readonly ITransactionRepository _transactions;
    private readonly IOrderService _orderService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IPaymentAccountService _paymentAccountService;
    private readonly INotificationService _notificationService;
    private readonly IPaymentNotificationService _paymentNotificationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentWebhookService> _logger;

    public PaymentWebhookService(
        ITransactionRepository transactions,
        IOrderService orderService,
        ISubscriptionService subscriptionService,
        IPaymentAccountService paymentAccountService,
        INotificationService notificationService,
        IPaymentNotificationService paymentNotificationService,
        IConfiguration configuration,
        ILogger<PaymentWebhookService> logger)
    {
        _transactions = transactions;
        _orderService = orderService;
        _subscriptionService = subscriptionService;
        _paymentAccountService = paymentAccountService;
        _notificationService = notificationService;
        _paymentNotificationService = paymentNotificationService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ProcessPayOsWebhookAsync(PayOsWebhookRequest request)
    {
        if (request == null || request.Data == null)
            throw new ArgumentException("Invalid payload.");

        var checksumKey = _configuration["PayOS:ChecksumKey"] ?? "YOUR_PAYOS_CHECKSUM_KEY";
        var isValid = VerifyPayOsSignature(request.Data, request.Signature, checksumKey);
        if (!isValid)
        {
            _logger.LogWarning("Invalid PayOS webhook signature.");
            throw new UnauthorizedAccessException("Signature verification failed.");
        }

        if (request.Code == "00")
        {
            var transaction = await FindTransactionFromTextAsync(request.Data.Description, request.Data.AccountNumber);
            if (transaction != null && transaction.Status == TransactionStatus.AwaitingPayment)
            {
                await _orderService.ConfirmPaymentAsync(transaction.TransactionId);
                await _paymentNotificationService.NotifyPaymentSuccessAsync(transaction.TransactionId.ToString());

                var msg = $"*Thanh toán VietQR thành công (PayOS)*\n" +
                          $"- Số tiền: +{request.Data.Amount:N0} VND\n" +
                          $"- Nội dung: {request.Data.Description}\n" +
                          $"- Mã đơn: `{transaction.TransactionCode}`";

                await SendNotificationsToOwnerAsync(transaction.BusinessId, "Giao dịch mới (+)", msg);
            }
            else
            {
                try
                {
                    await _subscriptionService.ProcessWebhookAsync(request.Data.OrderCode, request.Code);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi xử lý webhook đăng ký gói cước cho OrderCode {OrderCode}", request.Data.OrderCode);
                }
            }
        }
    }

    public async Task ProcessSePayIpnWebhookAsync(SePayWebhookRequest request, string authHeader)
    {
        if (request == null)
            throw new ArgumentException("Invalid payload.");

        var expectedApiKey = _configuration["SePay:ApiKey"] ?? "YOUR_SEPAY_API_KEY";
        if (string.IsNullOrEmpty(authHeader) || !authHeader.Equals($"Apikey {expectedApiKey}"))
        {
            _logger.LogWarning("[SePay IPN] Invalid Authorization header.");
            throw new UnauthorizedAccessException("Authorization failed.");
        }

        if (request.TransferType == "credit" && request.Amount > 0)
        {
            var transaction = await FindTransactionFromTextAsync(request.Content ?? "", request.AccountNumber);
            if (transaction != null)
            {
                if (transaction.Status == TransactionStatus.Completed)
                {
                    _logger.LogInformation("[SePay IPN] Giao dịch {Code} đã được hoàn tất trước đó. Bỏ qua.", transaction.TransactionCode);
                    return;
                }

                if (transaction.Status == TransactionStatus.AwaitingPayment && request.Amount >= transaction.TotalAmount)
                {
                    _logger.LogInformation("[SePay IPN] Khớp đơn hàng thành công. Tiến hành confirm đơn. TransactionId={Id}, Code={Code}, Amount={Amount}",
                        transaction.TransactionId, transaction.TransactionCode, request.Amount);

                    try
                    {
                        await _orderService.ConfirmPaymentAsync(transaction.TransactionId);
                        await _paymentNotificationService.NotifyPaymentSuccessAsync(transaction.TransactionId.ToString());

                        var msg = $"*Thanh toán VietQR thành công (SePay)*\n" +
                                  $"- Số tiền: +{request.Amount:N0} VND\n" +
                                  $"- Nội dung: {request.Content}\n" +
                                  $"- Cổng: {request.Gateway} ({request.AccountNumber})\n" +
                                  $"- Mã đơn: `{transaction.TransactionCode}`";

                        await SendNotificationsToOwnerAsync(transaction.BusinessId, "Giao dịch mới (+)", msg);
                    }
                    catch (ConflictException ex)
                    {
                        _logger.LogWarning(ex, "[SePay IPN] Đơn hàng {Code} đã được xác nhận thanh toán bởi luồng khác đồng thời. Trả về thành công.", transaction.TransactionCode);
                    }
                }
            }
            else
            {
                _logger.LogInformation("[SePay IPN] Nhận tiền vào {Amount} VND nhưng không tìm thấy đơn hàng khớp với nội dung: {Content}",
                    request.Amount, request.Content);
            }
        }
    }

    public async Task ProcessBankHubWebhookAsync(SePayBankHubEventRequest request, string secretKeyHeader)
    {
        if (request == null)
            throw new ArgumentException("Invalid payload.");

        var expectedSecretKey = _configuration["SePay:BankHub:SecretKey"] ?? "";
        if (string.IsNullOrEmpty(expectedSecretKey) || !secretKeyHeader.Equals(expectedSecretKey))
        {
            _logger.LogWarning("[BankHub Webhook] Invalid X-Secret-Key header.");
            throw new UnauthorizedAccessException("Authorization failed.");
        }

        _logger.LogInformation("[BankHub Webhook] Received event={Event}, xid={Xid}", request.Event, request.Xid);

        switch (request.Event?.ToUpperInvariant())
        {
            case "BANK_ACCOUNT_LINKED":
                await HandleBankAccountLinkedAsync(request);
                break;

            case "BANK_ACCOUNT_UNLINKED":
            case "BANK_ACCOUNT_INACTIVATED":
                await HandleBankAccountUnlinkedAsync(request);
                break;

            default:
                _logger.LogInformation("[BankHub Webhook] Unhandled event type: {Event}", request.Event);
                break;
        }
    }

    public async Task ProcessCassoWebhookAsync(CassoWebhookRequest request, string secureTokenHeader)
    {
        if (request == null || request.Data == null)
            throw new ArgumentException("Invalid payload.");

        var expectedToken = _configuration["Casso:SecureToken"] ?? "YOUR_CASSO_SECURE_TOKEN";
        if (string.IsNullOrEmpty(secureTokenHeader) || !secureTokenHeader.Equals(expectedToken))
        {
            _logger.LogWarning("Invalid Casso webhook secure token header.");
            throw new UnauthorizedAccessException("Authorization failed.");
        }

        foreach (var transactionData in request.Data)
        {
            if (transactionData.Amount > 0)
            {
                var transaction = await FindTransactionFromTextAsync(transactionData.Description, transactionData.BankSubAccId);
                if (transaction != null && transaction.Status == TransactionStatus.AwaitingPayment)
                {
                    await _orderService.ConfirmPaymentAsync(transaction.TransactionId);
                    await _paymentNotificationService.NotifyPaymentSuccessAsync(transaction.TransactionId.ToString());

                    var msg = $"*Thanh toán VietQR thành công (Casso)*\n" +
                              $"- Số tiền: +{transactionData.Amount:N0} VND\n" +
                              $"- Nội dung: {transactionData.Description}\n" +
                              $"- Người gửi: {transactionData.CorrespName}\n" +
                              $"- Mã đơn: `{transaction.TransactionCode}`";

                    await SendNotificationsToOwnerAsync(transaction.BusinessId, "Giao dịch mới (+)", msg);
                }
            }
        }
    }

    private async Task HandleBankAccountLinkedAsync(SePayBankHubEventRequest request)
    {
        var meta = request.Metadata;
        if (meta == null)
        {
            _logger.LogWarning("[BankHub] BANK_ACCOUNT_LINKED: metadata is null.");
            return;
        }

        var bankAccountXid = meta.BankAccountXid ?? "";
        var accountNumber  = meta.AccountNumber ?? "";
        var accountName    = meta.AccountHolderName ?? "";
        var bankName       = meta.BrandName ?? "";
        var linkTokenXid   = meta.LinkTokenXid ?? "";

        _logger.LogInformation(
            "[BankHub] LINKED: bank={BankName}, account={AccountNumber}, bankAccountXid={BankAccountXid}, linkTokenXid={LinkTokenXid}",
            bankName, accountNumber, bankAccountXid, linkTokenXid);

        // Gọi service xử lý cập nhật tài khoản liên kết
        await _paymentAccountService.CreateOrUpdateFromLinkTokenAsync(
            linkTokenXid,
            bankAccountXid,
            bankName,
            accountNumber,
            accountName
        );
    }

    private async Task HandleBankAccountUnlinkedAsync(SePayBankHubEventRequest request)
    {
        var meta = request.Metadata;
        if (meta == null || string.IsNullOrEmpty(meta.BankAccountXid))
        {
            _logger.LogWarning("[BankHub] BANK_ACCOUNT_UNLINKED: metadata or bankAccountXid is null.");
            return;
        }

        _logger.LogInformation(
            "[BankHub] UNLINKED: bankAccountXid={BankAccountXid}, account={AccountNumber}",
            meta.BankAccountXid, meta.AccountNumber);

        // Gọi service xóa tài khoản khỏi DB cục bộ
        await _paymentAccountService.DeleteBySePayBankAccountXidAsync(meta.BankAccountXid);
    }


    private async Task<Transaction?> FindTransactionFromTextAsync(string text, string? recipientAccountNumber = null)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var awaitingTransactions = await _transactions.GetAwaitingTransactionsWithPaymentsAsync();

        if (!string.IsNullOrEmpty(recipientAccountNumber))
        {
            var filteredTransactions = awaitingTransactions
                .Where(t => t.Payments.Any(p => p.PaymentAccount != null && 
                            p.PaymentAccount.AccountNumber.Equals(recipientAccountNumber, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (filteredTransactions.Any())
            {
                foreach (var t in filteredTransactions)
                {
                    if (text.Contains(t.TransactionCode, StringComparison.OrdinalIgnoreCase))
                    {
                        return t;
                    }
                }
            }
        }

        foreach (var t in awaitingTransactions)
        {
            if (text.Contains(t.TransactionCode, StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }
        }

        return null;
    }

    private async Task SendNotificationsToOwnerAsync(Guid businessId, string title, string message)
    {
        var telegramChatId = _configuration[$"Notification:Telegram:ChatId_{businessId}"] ?? _configuration["Notification:Telegram:DefaultChatId"];
        var fcmToken = _configuration[$"Notification:Fcm:Token_{businessId}"] ?? _configuration["Notification:Fcm:DefaultToken"];

        if (!string.IsNullOrEmpty(telegramChatId))
        {
            await _notificationService.SendTelegramAsync(telegramChatId, message);
        }

        if (!string.IsNullOrEmpty(fcmToken))
        {
            await _notificationService.SendFcmPushAsync(fcmToken, title, message);
        }
    }

    private bool VerifyPayOsSignature(PayOsData data, string expectedSignature, string checksumKey)
    {
        var sortedParams = new SortedDictionary<string, string>
        {
            { "accountNumber", data.AccountNumber },
            { "amount", data.Amount.ToString() },
            { "description", data.Description },
            { "orderCode", data.OrderCode.ToString() },
            { "paymentLinkId", data.PaymentLinkId },
            { "reference", data.Reference },
            { "transactionDateTime", data.TransactionDateTime }
        };

        var queryString = string.Join("&", sortedParams.Select(p => $"{p.Key}={p.Value}"));
        var keyBytes = Encoding.UTF8.GetBytes(checksumKey);
        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(queryString));
        var computedSignature = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

        return computedSignature == expectedSignature;
    }
}
