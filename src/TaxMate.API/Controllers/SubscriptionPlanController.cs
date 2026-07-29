using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.SubscriptionPlan;
using TaxMate.Service.Interfaces;
using SubscriptionPlanResponse = TaxMate.Model.DTO.SubscriptionPlanResponse;

namespace TaxMate.API.Controllers;

/// <summary>Quản lý gói đăng ký (Admin).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = UserRoles.Admin)]
public class SubscriptionPlanController : ControllerBase
{
    private readonly ISubscriptionPlanAdminService _subscriptionPlanAdminService;

    public SubscriptionPlanController(ISubscriptionPlanAdminService subscriptionPlanAdminService)
    {
        _subscriptionPlanAdminService = subscriptionPlanAdminService;
    }

    /// <summary>Danh sách tất cả gói đăng ký (kể cả đã tắt).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _subscriptionPlanAdminService.GetAllAsync(cancellationToken);
        return Ok(
            ApiResponse<IEnumerable<SubscriptionPlanResponse>>.Ok(
                result,
                "Get subscription plans successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Chi tiết gói đăng ký theo ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _subscriptionPlanAdminService.GetByIdAsync(id, cancellationToken);
        return Ok(
            ApiResponse<SubscriptionPlanResponse>.Ok(
                result,
                "Get subscription plan successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Tạo gói đăng ký mới.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateSubscriptionPlanRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _subscriptionPlanAdminService.CreateAsync(request, cancellationToken);
        return Created(
            $"api/SubscriptionPlan/{result.Id}",
            ApiResponse<SubscriptionPlanResponse>.Ok(
                result,
                "Subscription plan created successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Cập nhật gói đăng ký.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateSubscriptionPlanRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _subscriptionPlanAdminService.UpdateAsync(id, request, cancellationToken);
        return Ok(
            ApiResponse<SubscriptionPlanResponse>.Ok(
                result,
                "Subscription plan updated successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Bật/tắt gói đăng ký (IsActive).</summary>
    [HttpPatch("{id:guid}/toggle-active")]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken cancellationToken)
    {
        var result = await _subscriptionPlanAdminService.ToggleActiveAsync(id, cancellationToken);
        return Ok(
            ApiResponse<SubscriptionPlanResponse>.Ok(
                result,
                "Subscription plan status updated successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Xóa gói đăng ký (chỉ khi chưa có người đăng ký).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _subscriptionPlanAdminService.DeleteAsync(id, cancellationToken);
        return Ok(
            ApiResponse<object?>.Ok(
                null,
                "Subscription plan deleted successfully",
                HttpContext.TraceIdentifier));
    }
}
