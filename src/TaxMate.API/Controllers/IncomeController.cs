using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.Income;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IncomeController : ControllerBase
{
    private readonly IIncomeService _incomeService;

    public IncomeController(IIncomeService incomeService)
    {
        _incomeService = incomeService;
    }

    [HttpPost("business/{businessId:guid}")]
    public async Task<IActionResult> Create(
        Guid businessId,
        [FromBody] CreateIncomeRequest request)
    {
        var result = await _incomeService.CreateAsync(GetUserId(), businessId, request);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.IncomeId },
            ApiResponse<IncomeDTO>.Ok(
                result,
                "Income created successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateIncomeRequest request)
    {
        var result = await _incomeService.UpdateAsync(GetUserId(), id, request);
        return Ok(
            ApiResponse<IncomeDTO>.Ok(
                result,
                "Income updated successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _incomeService.DeleteAsync(GetUserId(), id);
        return Ok(
            ApiResponse<bool>.Ok(
                true,
                "Income deleted successfully",
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
        var result = await _incomeService.GetPagedAsync(
            GetUserId(), businessId, pageNumber, pageSize, search, fromDate, toDate, categoryId, paymentMethod);
        
        return Ok(
            ApiResponse<PagedResult<IncomeDTO>>.Ok(
                result,
                "Get paged incomes successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("business/{businessId:guid}/summary")]
    public async Task<IActionResult> GetMonthlySummary(
        Guid businessId,
        [FromQuery] int year,
        [FromQuery] int month)
    {
        var result = await _incomeService.GetMonthlySummaryAsync(GetUserId(), businessId, year, month);
        return Ok(
            ApiResponse<IncomeSummaryDTO>.Ok(
                result,
                "Get monthly summary successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _incomeService.GetByIdAsync(GetUserId(), id);
        return Ok(
            ApiResponse<IncomeDTO>.Ok(
                result,
                "Get income successfully",
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
