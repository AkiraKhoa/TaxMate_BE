using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = UserRoles.Owner)]
[Authorize(Policy = AuthPolicies.ActiveAccountOnly)]
public class SupplierController : ControllerBase
{
    private readonly ISupplierService _supplierService;

    public SupplierController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    [HttpPost("business/{businessId:guid}")]
    public async Task<IActionResult> Create(
        Guid businessId,
        [FromBody] CreateSupplierRequest request)
    {
        var result = await _supplierService.CreateAsync(GetUserId(), businessId, request);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ApiResponse<SupplierResponse>.Ok(
                result,
                "Tạo nhà cung cấp thành công.",
                HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSupplierRequest request)
    {
        var result = await _supplierService.UpdateAsync(GetUserId(), id, request);
        return Ok(
            ApiResponse<SupplierResponse>.Ok(
                result,
                "Cập nhật nhà cung cấp thành công.",
                HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _supplierService.DeleteAsync(GetUserId(), id);
        return Ok(
            ApiResponse<bool>.Ok(
                true,
                "Xóa nhà cung cấp thành công.",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("business/{businessId:guid}")]
    public async Task<IActionResult> GetByBusiness(Guid businessId)
    {
        var result = await _supplierService.GetByBusinessAsync(GetUserId(), businessId);
        return Ok(
            ApiResponse<IEnumerable<SupplierResponse>>.Ok(
                result,
                "Lấy danh sách nhà cung cấp thành công.",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _supplierService.GetByIdAsync(GetUserId(), id);
        return Ok(
            ApiResponse<SupplierResponse>.Ok(
                result,
                "Lấy thông tin nhà cung cấp thành công.",
                HttpContext.TraceIdentifier));
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (sub is null || !Guid.TryParse(sub, out var userId))
            throw new UnauthorizedAccessException("Token invalid.");

        return userId;
    }
}
