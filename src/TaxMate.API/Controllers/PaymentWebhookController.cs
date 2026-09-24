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

    // Auth: Authorization: Apikey <SePay:ApiKey>
    [HttpPost("sepay")]
    public async Task<IActionResult> HandleSePayWebhook(
        [FromBody] SePayWebhookRequest request)
    {
        var authHeader = Request.Headers.Authorization.ToString();
        await _webhookService.ProcessSePayIpnWebhookAsync(request, authHeader);
        return Ok(new { success = true });
    }

    // Auth: X-Secret-Key: <SePay:BankHub:SecretKey>
    [HttpPost("bankhub")]
    public async Task<IActionResult> HandleBankHubWebhook(
        [FromBody] SePayBankHubEventRequest request)
    {
        var secretKeyHeader = Request.Headers["X-Secret-Key"].ToString();
        await _webhookService.ProcessBankHubWebhookAsync(request, secretKeyHeader);
        return Ok(new { success = true });
    }
}
