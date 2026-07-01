using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/webhook/payment")]
[AllowAnonymous]
public class PaymentWebhookController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IGenericRepository<Transaction> _transactions;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentWebhookController> _logger;

    public PaymentWebhookController(
        IOrderService orderService,
        IGenericRepository<Transaction> transactions,
        INotificationService notificationService,
        IConfiguration configuration,
        ILogger<PaymentWebhookController> logger)
    {
        _orderService = orderService;
        _transactions = transactions;
        _notificationService = notificationService;
        _configuration = configuration;
        _logger = logger;
    }

    // ================= 1. WEBHOOK PAYOS =================
    [HttpPost("payos")]
    public async Task<IActionResult> HandlePayOsWebhook([FromBody] PayOsWebhookRequest request)
    {
        if (request == null || request.Data == null) return BadRequest("Invalid payload.");

        var checksumKey = _configuration["PayOS:ChecksumKey"] ?? "YOUR_PAYOS_CHECKSUM_KEY";
        var isValid = VerifyPayOsSignature(request.Data, request.Signature, checksumKey);
        if (!isValid)
        {
            _logger.LogWarning("Invalid PayOS webhook signature.");
            return Unauthorized("Signature verification failed.");
        }

        if (request.Code == "00")
        {
            // Trích xuất mã đơn hàng từ Description (ví dụ: "Thanh toan don hang TX-xxxx-xxx")
            var transaction = await FindTransactionFromTextAsync(request.Data.Description);
            if (transaction != null && transaction.Status == TransactionStatus.AwaitingPayment)
            {
                await _orderService.ConfirmPaymentAsync(transaction.TransactionId);

                // Gửi thông báo cho chủ cửa hàng
                var msg = $"🔔 *Thanh toán VietQR thành công (PayOS)*\n" +
                          $"💰 Số tiền: +{request.Data.Amount:N0} VND\n" +
                          $"📝 Nội dung: {request.Data.Description}\n" +
                          $"🔑 Mã đơn: `{transaction.TransactionCode}`";

                await SendNotificationsToOwnerAsync(transaction.BusinessId, "Giao dịch mới (+)", msg);
            }
        }

        return Ok(new { success = true });
    }

    // ================= 2. WEBHOOK SEPAY =================
    [HttpPost("sepay")]
    public async Task<IActionResult> HandleSePayWebhook([FromBody] SePayWebhookRequest request)
    {
        if (request == null) return BadRequest("Invalid payload.");

        var expectedApiKey = _configuration["SePay:ApiKey"] ?? "YOUR_SEPAY_API_KEY";
        var authHeader = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.Equals($"Apikey {expectedApiKey}"))
        {
            _logger.LogWarning("Invalid SePay webhook authorization header.");
            return Unauthorized("Authorization failed.");
        }

        if (request.TransferType == "in" && request.TransferAmount > 0)
        {
            var transaction = await FindTransactionFromTextAsync(request.Content);
            if (transaction != null && transaction.Status == TransactionStatus.AwaitingPayment)
            {
                await _orderService.ConfirmPaymentAsync(transaction.TransactionId);

                var msg = $"🔔 *Thanh toán VietQR thành công (SePay)*\n" +
                          $"💰 Số tiền: +{request.TransferAmount:N0} VND\n" +
                          $"📝 Nội dung: {request.Content}\n" +
                          $"🏦 Cổng: {request.Gateway} ({request.AccountNumber})\n" +
                          $"🔑 Mã đơn: `{transaction.TransactionCode}`";

                await SendNotificationsToOwnerAsync(transaction.BusinessId, "Giao dịch mới (+)", msg);
            }
        }

        return Ok(new { success = true });
    }

    // ================= 3. WEBHOOK CASSO =================
    [HttpPost("casso")]
    public async Task<IActionResult> HandleCassoWebhook([FromBody] CassoWebhookRequest request)
    {
        if (request == null || request.Data == null) return BadRequest("Invalid payload.");

        var expectedToken = _configuration["Casso:SecureToken"] ?? "YOUR_CASSO_SECURE_TOKEN";
        var secureToken = Request.Headers["Secure-Token"].ToString();
        if (string.IsNullOrEmpty(secureToken) || !secureToken.Equals(expectedToken))
        {
            _logger.LogWarning("Invalid Casso webhook secure token header.");
            return Unauthorized("Authorization failed.");
        }

        foreach (var transactionData in request.Data)
        {
            if (transactionData.Amount > 0)
            {
                var transaction = await FindTransactionFromTextAsync(transactionData.Description);
                if (transaction != null && transaction.Status == TransactionStatus.AwaitingPayment)
                {
                    await _orderService.ConfirmPaymentAsync(transaction.TransactionId);

                    var msg = $"🔔 *Thanh toán VietQR thành công (Casso)*\n" +
                              $"💰 Số tiền: +{transactionData.Amount:N0} VND\n" +
                              $"📝 Nội dung: {transactionData.Description}\n" +
                              $"👤 Người gửi: {transactionData.CorrespName}\n" +
                              $"🔑 Mã đơn: `{transaction.TransactionCode}`";

                    await SendNotificationsToOwnerAsync(transaction.BusinessId, "Giao dịch mới (+)", msg);
                }
            }
        }

        return Ok(new { success = true, error = 0, message = "Ok" });
    }

    private async Task<Transaction?> FindTransactionFromTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        // Tìm tất cả Transaction đang chờ thanh toán của hệ thống
        var awaitingTransactions = await _transactions.FindAsync(x => x.Status == TransactionStatus.AwaitingPayment);

        // Duyệt tìm mã đơn hàng trong nội dung chuyển tiền (không phân biệt hoa thường)
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
        // Trong thực tế sẽ lấy Telegram ChatId và FCM Device Token của chủ shop sở hữu business này
        // Ví dụ giả lập gửi về chatId và token lưu trong cấu hình hệ thống:
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

// ================= DTOs =================
public class PayOsWebhookRequest
{
    [JsonPropertyName("code")] public string Code { get; set; } = null!;
    [JsonPropertyName("desc")] public string Desc { get; set; } = null!;
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("data")] public PayOsData Data { get; set; } = null!;
    [JsonPropertyName("signature")] public string Signature { get; set; } = null!;
}

public class PayOsData
{
    [JsonPropertyName("orderCode")] public long OrderCode { get; set; }
    [JsonPropertyName("amount")] public int Amount { get; set; }
    [JsonPropertyName("description")] public string Description { get; set; } = null!;
    [JsonPropertyName("accountNumber")] public string AccountNumber { get; set; } = null!;
    [JsonPropertyName("reference")] public string Reference { get; set; } = null!;
    [JsonPropertyName("transactionDateTime")] public string TransactionDateTime { get; set; } = null!;
    [JsonPropertyName("currency")] public string Currency { get; set; } = null!;
    [JsonPropertyName("paymentLinkId")] public string PaymentLinkId { get; set; } = null!;
}

public class SePayWebhookRequest
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("gateway")] public string Gateway { get; set; } = null!;
    [JsonPropertyName("transactionDate")] public string TransactionDate { get; set; } = null!;
    [JsonPropertyName("accountNumber")] public string AccountNumber { get; set; } = null!;
    [JsonPropertyName("subAccount")] public string SubAccount { get; set; } = null!;
    [JsonPropertyName("code")] public string Code { get; set; } = null!;
    [JsonPropertyName("content")] public string Content { get; set; } = null!;
    [JsonPropertyName("transferType")] public string TransferType { get; set; } = null!; // "in" / "out"
    [JsonPropertyName("transferAmount")] public decimal TransferAmount { get; set; }
    [JsonPropertyName("accumulated")] public decimal Accumulated { get; set; }
    [JsonPropertyName("referenceCode")] public string ReferenceCode { get; set; } = null!;
    [JsonPropertyName("description")] public string Description { get; set; } = null!;
}

public class CassoWebhookRequest
{
    [JsonPropertyName("error")] public int Error { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = null!;
    [JsonPropertyName("data")] public List<CassoData> Data { get; set; } = null!;
}

public class CassoData
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("tid")] public string Tid { get; set; } = null!;
    [JsonPropertyName("description")] public string Description { get; set; } = null!;
    [JsonPropertyName("amount")] public decimal Amount { get; set; }
    [JsonPropertyName("cusumBalance")] public decimal CusumBalance { get; set; }
    [JsonPropertyName("when")] public string When { get; set; } = null!;
    [JsonPropertyName("bookingDate")] public string BookingDate { get; set; } = null!;
    [JsonPropertyName("bankSubAccId")] public string BankSubAccId { get; set; } = null!;
    [JsonPropertyName("correspName")] public string CorrespName { get; set; } = null!;
    [JsonPropertyName("correspAccId")] public string CorrespAccId { get; set; } = null!;
    [JsonPropertyName("correspBankName")] public string CorrespBankName { get; set; } = null!;
    [JsonPropertyName("correspBankId")] public string CorrespBankId { get; set; } = null!;
}
