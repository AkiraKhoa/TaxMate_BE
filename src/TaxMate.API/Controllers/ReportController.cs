using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.Reports;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/businesses/{businessId:guid}/reports")]
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("sales-dashboard")]
    public async Task<IActionResult> GetSalesDashboard(
        Guid businessId,
        [FromQuery] int year,
        [FromQuery] int month)
    {
        var result = await _reportService.GetSalesDashboardAsync(
            businessId,
            year,
            month);

        return Ok(
            ApiResponse<SalesDashboardResponse>.Ok(
                result,
                "Get sales dashboard successfully",
                HttpContext.TraceIdentifier));
    }
}