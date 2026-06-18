using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    /// <summary>Gets all active subscription plans with their features.</summary>
    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans()
    {
        var result = await _subscriptionService.GetActivePlansAsync();
        return Ok(
            ApiResponse<IEnumerable<SubscriptionPlanResponse>>.Ok(
                result,
                "Subscription plans retrieved successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Gets the current active subscription for a specific user.</summary>
    [HttpGet("user/{userId:guid}/current")]
    public async Task<IActionResult> GetCurrent(Guid userId)
    {
        var result = await _subscriptionService.GetCurrentSubscriptionAsync(userId);
        return Ok(
            ApiResponse<UserSubscriptionResponse?>.Ok(
                result,
                "Current user subscription retrieved successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Initiates a new user subscription and generates a PayOS payment link.</summary>
    [HttpPost("user/{userId:guid}/subscribe")]
    public async Task<IActionResult> Subscribe(Guid userId, [FromBody] SubscribeRequest request)
    {
        var result = await _subscriptionService.SubscribeAsync(userId, request);
        return Ok(
            ApiResponse<SubscribeResponse>.Ok(
                result,
                "Subscription initiated successfully. Please proceed with payment using checkoutUrl.",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Disables auto-renewal for a user's active subscription.</summary>
    [HttpPost("user/{userId:guid}/cancel-renew")]
    public async Task<IActionResult> CancelRenew(Guid userId)
    {
        await _subscriptionService.CancelAutoRenewAsync(userId);
        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Subscription auto-renew disabled successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Cancels a user's active subscription immediately.</summary>
    [HttpPost("user/{userId:guid}/cancel")]
    public async Task<IActionResult> CancelImmediately(Guid userId)
    {
        await _subscriptionService.CancelSubscriptionImmediatelyAsync(userId);
        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Subscription cancelled immediately",
                HttpContext.TraceIdentifier));
    }
}
