using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.TaxFiling;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/tax-filing-tasks/business/{businessId:guid}")]
public sealed class TaxFilingTaskController : ControllerBase
{
    private readonly ITaxFilingScheduleService _schedule;

    public TaxFilingTaskController(ITaxFilingScheduleService schedule)
    {
        _schedule = schedule;
    }

    [HttpGet]
    public async Task<IActionResult> GetTasks(
        Guid businessId,
        [FromQuery] int year,
        CancellationToken cancellationToken)
    {
        var result = await _schedule.GetTasksAsync(
            GetUserId(), businessId, year, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TaxFilingTaskSummaryResponse>>.Ok(
            result,
            "Đã tải danh sách việc cần làm về thuế.",
            HttpContext.TraceIdentifier));
    }

    [HttpPost("{taskId}/open")]
    public async Task<IActionResult> OpenTask(
        Guid businessId,
        string taskId,
        CancellationToken cancellationToken)
    {
        var result = await _schedule.OpenTaskAsync(
            GetUserId(), businessId, taskId, cancellationToken);
        return Ok(ApiResponse<TaxFilingTaskSummaryResponse>.Ok(
            result,
            "Đã mở hồ sơ thông báo doanh thu.",
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
