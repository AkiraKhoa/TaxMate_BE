using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.Expense;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = UserRoles.Owner)]
[Authorize(Policy = AuthPolicies.ActiveAccountOnly)]
public class ExpenseController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public ExpenseController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    [HttpPost("business/{businessId:guid}")]
    public async Task<IActionResult> Create(
        Guid businessId,
        [FromBody] CreateExpenseRequest request)
    {
        var result = await _expenseService.CreateAsync(GetUserId(), businessId, request);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.ExpenseId },
            ApiResponse<ExpenseDTO>.Ok(
                result,
                "Expense created successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExpenseRequest request)
    {
        var result = await _expenseService.UpdateAsync(GetUserId(), id, request);
        return Ok(
            ApiResponse<ExpenseDTO>.Ok(
                result,
                "Expense updated successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _expenseService.DeleteAsync(GetUserId(), id);
        return Ok(
            ApiResponse<bool>.Ok(
                true,
                "Expense deleted successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("business/{businessId:guid}")]
    public async Task<IActionResult> GetPaged(
        Guid businessId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? paymentMethod = null)
    {
        var result = await _expenseService.GetPagedAsync(
            GetUserId(), businessId, pageNumber, pageSize, search, fromDate, toDate, categoryId, paymentMethod);
        
        return Ok(
            ApiResponse<PagedResult<ExpenseDTO>>.Ok(
                result,
                "Get paged expenses successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("business/{businessId:guid}/summary")]
    public async Task<IActionResult> GetMonthlySummary(
        Guid businessId,
        [FromQuery] int year,
        [FromQuery] int month)
    {
        var result = await _expenseService.GetMonthlySummaryAsync(GetUserId(), businessId, year, month);
        return Ok(
            ApiResponse<ExpenseSummaryDTO>.Ok(
                result,
                "Get monthly summary successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _expenseService.GetByIdAsync(GetUserId(), id);
        return Ok(
            ApiResponse<ExpenseDTO>.Ok(
                result,
                "Get expense successfully",
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
