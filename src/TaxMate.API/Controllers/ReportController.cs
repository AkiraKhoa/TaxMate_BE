using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.Reports;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/businesses/reports")]
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("{businessId:guid}/sales-dashboard")]
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

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetBusinesses(Guid userId)
    {
        var businesses =
            await _reportService.GetBusinessesAsync(userId);

        return Ok(
            ApiResponse<List<BusinessProfileDropdownResponse>>.Ok(
                businesses,
                "Get businesses successfully",
                HttpContext.TraceIdentifier));
    }
    
    [HttpGet("{businessId:guid}/active-months")]
    public async Task<IActionResult> GetActiveSalesMonths(Guid businessId)
    {
        var result = await _reportService.GetActiveSalesMonthsAsync(
            businessId);

        return Ok(
            ApiResponse<List<ActiveSalesMonthResponse>>.Ok(
                result,
                "Get active sales months successfully",
                HttpContext.TraceIdentifier));
    }
}