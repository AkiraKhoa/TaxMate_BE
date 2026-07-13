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

    public PaymentWebhookController(IPaymentWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    // ================= 1. WEBHOOK PAYOS =================
    [HttpPost("payos")]
    public async Task<IActionResult> HandlePayOsWebhook([FromBody] PayOsWebhookRequest request)
    {
        await _webhookService.ProcessPayOsWebhookAsync(request);
        return Ok(new { success = true });
    }

    // ================= 2. WEBHOOK SEPAY IPN (Biến động số dư) =================
    // Auth: Authorization: Apikey <SePay:ApiKey>
    [HttpPost("sepay")]
    public async Task<IActionResult> HandleSePayWebhook([FromBody] SePayWebhookRequest request)
    {
        var authHeader = Request.Headers["Authorization"].ToString();
        await _webhookService.ProcessSePayIpnWebhookAsync(request, authHeader);
        return Ok(new { success = true });
    }

    // ================= 3. WEBHOOK SEPAY BANK HUB (Liên kết tài khoản ngân hàng) =================
    // Auth: X-Secret-Key: <SePay:BankHub:SecretKey>
    [HttpPost("bankhub")]
    public async Task<IActionResult> HandleBankHubWebhook([FromBody] SePayBankHubEventRequest request)
    {
        var secretKeyHeader = Request.Headers["X-Secret-Key"].ToString();
        await _webhookService.ProcessBankHubWebhookAsync(request, secretKeyHeader);
        return Ok(new { success = true });
    }

    // ================= 4. WEBHOOK CASSO =================
    [HttpPost("casso")]
    public async Task<IActionResult> HandleCassoWebhook([FromBody] CassoWebhookRequest request)
    {
        var secureTokenHeader = Request.Headers["Secure-Token"].ToString();
        await _webhookService.ProcessCassoWebhookAsync(request, secureTokenHeader);
        return Ok(new { success = true, error = 0, message = "Ok" });
    }
}
