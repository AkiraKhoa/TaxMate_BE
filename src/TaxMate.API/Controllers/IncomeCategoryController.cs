using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.IncomeCategory;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IncomeCategoryController : ControllerBase
{
    private readonly IIncomeCategoryService _incomeCategoryService;

    public IncomeCategoryController(IIncomeCategoryService incomeCategoryService)
    {
        _incomeCategoryService = incomeCategoryService;
    }

    [HttpPost("business/{businessId:guid}")]
    public async Task<IActionResult> Create(
        Guid businessId,
        [FromBody] CreateIncomeCategoryRequest request)
    {
        var result = await _incomeCategoryService.CreateAsync(GetUserId(), businessId, request);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.IncomeCategoryId },
            ApiResponse<IncomeCategoryDTO>.Ok(
                result,
                "Income category created successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateIncomeCategoryRequest request)
    {
        var result = await _incomeCategoryService.UpdateAsync(GetUserId(), id, request);
        return Ok(
            ApiResponse<IncomeCategoryDTO>.Ok(
                result,
                "Income category updated successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _incomeCategoryService.DeleteAsync(GetUserId(), id);
        return Ok(
            ApiResponse<bool>.Ok(
                true,
                "Income category deleted successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("business/{businessId:guid}")]
    public async Task<IActionResult> GetByBusiness(Guid businessId)
    {
        var result = await _incomeCategoryService.GetByBusinessAsync(GetUserId(), businessId);
        return Ok(
            ApiResponse<IEnumerable<IncomeCategoryDTO>>.Ok(
                result,
                "Get income categories successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _incomeCategoryService.GetByIdAsync(GetUserId(), id);
        return Ok(
            ApiResponse<IncomeCategoryDTO>.Ok(
                result,
                "Get income category successfully",
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
