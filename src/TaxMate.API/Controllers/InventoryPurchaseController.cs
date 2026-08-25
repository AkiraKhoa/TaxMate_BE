using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.InventoryPurchase;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/inventory-purchases")]
[Authorize(Roles = UserRoles.Owner)]
[Authorize(Policy = AuthPolicies.ActiveAccountOnly)]
public sealed class InventoryPurchaseController : ControllerBase
{
    private readonly IInventoryPurchaseService _service;

    public InventoryPurchaseController(IInventoryPurchaseService service)
    {
        _service = service;
    }

    [HttpPost("business/{businessId:guid}")]
    public async Task<IActionResult> Create(
        Guid businessId,
        [FromBody] CreateInventoryPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(
            GetUserId(),
            businessId,
            request,
            cancellationToken);
        return CreatedAtAction(
            nameof(GetById),
            new { expenseId = result.ExpenseId },
            ApiResponse<InventoryPurchaseResponse>.Ok(
                result,
                "Phiếu nhập đã được tạo.",
                HttpContext.TraceIdentifier));
    }

    [HttpPut("{expenseId:guid}")]
    public async Task<IActionResult> Update(
        Guid expenseId,
        [FromBody] UpdateInventoryPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(
            GetUserId(),
            expenseId,
            request,
            cancellationToken);
        return Ok(ApiResponse<InventoryPurchaseResponse>.Ok(
            result,
            "Phiếu nhập đã được cập nhật.",
            HttpContext.TraceIdentifier));
    }

    [HttpDelete("{expenseId:guid}")]
    public async Task<IActionResult> Delete(
        Guid expenseId,
        CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(GetUserId(), expenseId, cancellationToken);
        return Ok(ApiResponse<string>.Ok(
            "Success",
            "Phiếu nhập đã được xóa.",
            HttpContext.TraceIdentifier));
    }

    [HttpGet("{expenseId:guid}")]
    public async Task<IActionResult> GetById(
        Guid expenseId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(
            GetUserId(),
            expenseId,
            cancellationToken);
        return Ok(ApiResponse<InventoryPurchaseResponse>.Ok(
            result,
            "Đã tải phiếu nhập.",
            HttpContext.TraceIdentifier));
    }

    [HttpGet("business/{businessId:guid}")]
    public async Task<IActionResult> GetPaged(
        Guid businessId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetPagedAsync(
            GetUserId(),
            businessId,
            pageNumber,
            pageSize,
            cancellationToken);
        return Ok(ApiResponse<PagedResult<InventoryPurchaseResponse>>.Ok(
            result,
            "Đã tải danh sách phiếu nhập.",
            HttpContext.TraceIdentifier));
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (sub is null || !Guid.TryParse(sub, out var userId))
        {
            throw new UnauthorizedAccessException("Token invalid.");
        }

        return userId;
    }
}
