using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayOS.Models.Webhooks;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(ISubscriptionService subscriptionService, ILogger<PaymentController> logger)
    {
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    /// <summary>Receives and processes PayOS payment webhook notifications.</summary>
    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] Webhook body)
    {
        try
        {
            var webhookData = await _subscriptionService.VerifyWebhookDataAsync(body);

            // PayOS test event orderCode is 123
            if (webhookData.OrderCode == 123)
            {
                _logger.LogInformation("Received PayOS test event. Ignoring.");
                return Ok(new { success = true, message = "Test event ignored." });
            }

            _logger.LogInformation("Processing PayOS webhook. OrderCode: {OrderCode}, Code: {Code}", webhookData.OrderCode, webhookData.Code);
            await _subscriptionService.ProcessWebhookAsync(webhookData.OrderCode, webhookData.Code);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PayOS webhook.");
            // PayOS requires 200 OK responses to prevent retries.
            return Ok(new { success = false, error = ex.Message });
        }
    }
}
