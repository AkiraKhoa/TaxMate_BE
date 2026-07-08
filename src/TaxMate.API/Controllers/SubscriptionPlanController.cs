using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.SubscriptionPlan;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

/// <summary>Quản lý gói subscription (Admin).</summary>
[ApiController]
[Route("api/[controller]")]
// [Authorize(Policy = AuthPolicies.ActiveAccountOnly)]
// [Authorize(Roles = UserRoles.Admin)]
public class SubscriptionPlanController : ControllerBase
{
    private readonly ISubscriptionPlanService _subscriptionPlanService;

    public SubscriptionPlanController(
        ISubscriptionPlanService subscriptionPlanService)
    {
        _subscriptionPlanService = subscriptionPlanService;
    }

    /// <summary>Tạo gói subscription mới.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateSubscriptionPlanRequest request)
    {
        var id = await _subscriptionPlanService.CreateAsync(request);

        return Created(
            $"api/SubscriptionPlan/{id}",
            ApiResponse<Guid>.Ok(
                id,
                "Subscription plan created successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Lấy danh sách gói subscription có phân trang.</summary>
    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool? isActive = null)
    {
        var result = await _subscriptionPlanService
            .GetPagedAsync(page, pageSize, isActive);

        return Ok(
            ApiResponse<PagedResult<SubscriptionPlanResponse>>.Ok(
                result,
                "Get subscription plans successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Lấy chi tiết gói subscription theo ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _subscriptionPlanService.GetByIdAsync(id);

        return Ok(
            ApiResponse<SubscriptionPlanResponse>.Ok(
                result,
                "Get subscription plan successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Cập nhật thông tin gói subscription.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateSubscriptionPlanRequest request)
    {
        await _subscriptionPlanService.UpdateAsync(id, request);

        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Subscription plan updated successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Vô hiệu hóa gói subscription.</summary>
    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await _subscriptionPlanService.DeactivateAsync(id);

        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Subscription plan deactivated successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Kích hoạt lại gói subscription.</summary>
    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        await _subscriptionPlanService.ActivateAsync(id);

        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Subscription plan activated successfully",
                HttpContext.TraceIdentifier));
    }
}