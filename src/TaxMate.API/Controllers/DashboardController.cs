using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.Dashboard;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

/// <summary>Admin dashboard analytics.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = UserRoles.Admin)]
public class DashboardController : ControllerBase
{
    private readonly IDashboardAnalyticsService _dashboardAnalyticsService;

    public DashboardController(IDashboardAnalyticsService dashboardAnalyticsService)
    {
        _dashboardAnalyticsService = dashboardAnalyticsService;
    }

    /// <summary>Total active businesses: this month vs last month.</summary>
    [HttpGet("active-businesses")]
    public async Task<IActionResult> GetActiveBusinesses(CancellationToken cancellationToken)
    {
        var result = await _dashboardAnalyticsService.GetActiveBusinessesAsync(cancellationToken);
        return Ok(ApiResponse<MomCountMetricDto>.Ok(
            result,
            "Get active businesses successfully",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Paid subscriptions: this month vs last month.</summary>
    [HttpGet("paid-subscriptions")]
    public async Task<IActionResult> GetPaidSubscriptions(CancellationToken cancellationToken)
    {
        var result = await _dashboardAnalyticsService.GetPaidSubscriptionsAsync(cancellationToken);
        return Ok(ApiResponse<MomCountMetricDto>.Ok(
            result,
            "Get paid subscriptions successfully",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Monthly subscription revenue: this month vs last month.</summary>
    [HttpGet("monthly-revenue")]
    public async Task<IActionResult> GetMonthlyRevenue(CancellationToken cancellationToken)
    {
        var result = await _dashboardAnalyticsService.GetMonthlyRevenueAsync(cancellationToken);
        return Ok(ApiResponse<MomRevenueMetricDto>.Ok(
            result,
            "Get monthly revenue successfully",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Subscription trend for the recent 6 months.</summary>
    [HttpGet("subscription-trend")]
    public async Task<IActionResult> GetSubscriptionTrend(CancellationToken cancellationToken)
    {
        var result = await _dashboardAnalyticsService.GetSubscriptionTrendAsync(cancellationToken);
        return Ok(ApiResponse<SubscriptionTrendResponseDto>.Ok(
            result,
            "Get subscription trend successfully",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Service package distribution for the recent 6 months.</summary>
    [HttpGet("service-package-distribution")]
    public async Task<IActionResult> GetServicePackageDistribution(CancellationToken cancellationToken)
    {
        var result = await _dashboardAnalyticsService.GetServicePackageDistributionAsync(cancellationToken);
        return Ok(ApiResponse<ServicePackageDistributionResponseDto>.Ok(
            result,
            "Get service package distribution successfully",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Package revenue breakdown for the current month.</summary>
    [HttpGet("package-revenue")]
    public async Task<IActionResult> GetPackageRevenue(CancellationToken cancellationToken)
    {
        var result = await _dashboardAnalyticsService.GetPackageRevenueAsync(cancellationToken);
        return Ok(ApiResponse<PackageRevenueResponseDto>.Ok(
            result,
            "Get package revenue successfully",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Business user trend for the recent 6 months.</summary>
    [HttpGet("business-user-trend")]
    public async Task<IActionResult> GetBusinessUserTrend(CancellationToken cancellationToken)
    {
        var result = await _dashboardAnalyticsService.GetBusinessUserTrendAsync(cancellationToken);
        return Ok(ApiResponse<BusinessUserTrendResponseDto>.Ok(
            result,
            "Get business user trend successfully",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Total assistant chat messages (all-time).</summary>
    [HttpGet("total-chat-messages")]
    public async Task<IActionResult> GetTotalChatMessages(CancellationToken cancellationToken)
    {
        var result = await _dashboardAnalyticsService.GetTotalChatMessagesAsync(cancellationToken);
        return Ok(ApiResponse<ChatMessageCountDto>.Ok(
            result,
            "Get total chat messages successfully",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Assistant chat messages created today (UTC).</summary>
    [HttpGet("today-chat-messages")]
    public async Task<IActionResult> GetTodayChatMessages(CancellationToken cancellationToken)
    {
        var result = await _dashboardAnalyticsService.GetTodayChatMessagesAsync(cancellationToken);
        return Ok(ApiResponse<ChatMessageCountDto>.Ok(
            result,
            "Get today chat messages successfully",
            HttpContext.TraceIdentifier));
    }

    /// <summary>AI accuracy from average RAG similarity score (0-100%).</summary>
    [HttpGet("ai-accuracy")]
    public async Task<IActionResult> GetAiAccuracy(CancellationToken cancellationToken)
    {
        var result = await _dashboardAnalyticsService.GetAiAccuracyAsync(cancellationToken);
        return Ok(ApiResponse<AiAccuracyMetricDto>.Ok(
            result,
            "Get AI accuracy successfully",
            HttpContext.TraceIdentifier));
    }

    /// <summary>User conversion funnel: total owners and active users per subscription plan.</summary>
    [HttpGet("user-conversion")]
    public async Task<IActionResult> GetUserConversion(CancellationToken cancellationToken)
    {
        var result = await _dashboardAnalyticsService.GetUserConversionAsync(cancellationToken);
        return Ok(ApiResponse<UserConversionResponseDto>.Ok(
            result,
            "Get user conversion successfully",
            HttpContext.TraceIdentifier));
    }
}
