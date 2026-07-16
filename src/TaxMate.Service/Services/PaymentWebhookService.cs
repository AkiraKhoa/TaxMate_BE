using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly IPaymentAccountService _paymentAccountService;
    private readonly INotificationService _notificationService;
    private readonly IPaymentNotificationService _paymentNotificationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentWebhookService> _logger;

    public PaymentWebhookService(
        ITransactionRepository transactions,
        IOrderService orderService,
        IPaymentAccountService paymentAccountService,
        INotificationService notificationService,
        IPaymentNotificationService paymentNotificationService,
        IConfiguration configuration,
        ILogger<PaymentWebhookService> logger)
    {
        _transactions = transactions;
        _orderService = orderService;
        _paymentAccountService = paymentAccountService;
        _notificationService = notificationService;
        _paymentNotificationService = paymentNotificationService;
        _configuration = configuration;
        _logger = logger;
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
}
