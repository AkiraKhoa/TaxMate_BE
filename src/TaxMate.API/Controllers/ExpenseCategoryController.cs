using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.ExpenseCategory;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExpenseCategoryController : ControllerBase
{
    private readonly IExpenseCategoryService _expenseCategoryService;

    public ExpenseCategoryController(IExpenseCategoryService expenseCategoryService)
    {
        _expenseCategoryService = expenseCategoryService;
    }

    [HttpPost("business/{businessId:guid}")]
    public async Task<IActionResult> Create(
        Guid businessId,
        [FromBody] CreateExpenseCategoryRequest request)
    {
        var result = await _expenseCategoryService.CreateAsync(GetUserId(), businessId, request);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.ExpenseCategoryId },
            ApiResponse<ExpenseCategoryDTO>.Ok(
                result,
                "Expense category created successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExpenseCategoryRequest request)
    {
        var result = await _expenseCategoryService.UpdateAsync(GetUserId(), id, request);
        return Ok(
            ApiResponse<ExpenseCategoryDTO>.Ok(
                result,
                "Expense category updated successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _expenseCategoryService.DeleteAsync(GetUserId(), id);
        return Ok(
            ApiResponse<bool>.Ok(
                true,
                "Expense category deleted successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("business/{businessId:guid}")]
    public async Task<IActionResult> GetByBusiness(Guid businessId)
    {
        var result = await _expenseCategoryService.GetByBusinessAsync(GetUserId(), businessId);
        return Ok(
            ApiResponse<IEnumerable<ExpenseCategoryDTO>>.Ok(
                result,
                "Get expense categories successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _expenseCategoryService.GetByIdAsync(GetUserId(), id);
        return Ok(
            ApiResponse<ExpenseCategoryDTO>.Ok(
                result,
                "Get expense category successfully",
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
