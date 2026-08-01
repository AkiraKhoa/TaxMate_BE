using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.DTO;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/webhook/payment")]
[AllowAnonymous]
public class PaymentWebhookController : ControllerBase
{
    private readonly IPaymentWebhookService _webhookService;
    private readonly ILogger<PaymentWebhookController> _logger;

    public PaymentWebhookController(IPaymentWebhookService webhookService, ILogger<PaymentWebhookController> logger)
    {
        _webhookService = webhookService;
        _logger = logger;
    }

    // ================= 1. WEBHOOK SEPAY IPN (Biến động số dư) =================
    // Auth: Authorization: Apikey <SePay:ApiKey>
    [HttpPost("sepay")]
    public async Task<IActionResult> HandleSePayWebhook([FromBody] SePayWebhookRequest request)
    {
        var authHeader = Request.Headers["Authorization"].ToString();
        await _webhookService.ProcessSePayIpnWebhookAsync(request, authHeader);
        return Ok(new { success = true });
    }

    // ================= 2. WEBHOOK SEPAY BANK HUB (Liên kết tài khoản ngân hàng) =================
    // Auth: X-Secret-Key: <SePay:BankHub:SecretKey>
    [HttpPost("bankhub")]
    public async Task<IActionResult> HandleBankHubWebhook()
    {
        // --- DEBUG: Đọc raw body để log trước khi parse ---
        Request.EnableBuffering();
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            rawBody = await reader.ReadToEndAsync();
            Request.Body.Position = 0;
        }

        var secretKeyHeader = Request.Headers["X-Secret-Key"].ToString();
        var contentType = Request.ContentType ?? "(none)";

        _logger.LogInformation(
            "[BankHub Webhook DEBUG] ContentType={ContentType} | X-Secret-Key={SecretKey} | Body={Body}",
            contentType, secretKeyHeader, rawBody);

        // --- Parse thủ công để tránh 400 tự động từ model binding ---
        SePayBankHubEventRequest? request = null;
        try
        {
            request = JsonSerializer.Deserialize<SePayBankHubEventRequest>(
                rawBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[BankHub Webhook DEBUG] Failed to deserialize body: {Error}", ex.Message);
        }

        if (request != null)
        {
            await _webhookService.ProcessBankHubWebhookAsync(request, secretKeyHeader);
        }

        return Ok(new { success = true });
    }
}

