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
    
    [HttpGet("{businessId:guid}/estimated-profit-dashboard")]
    public async Task<IActionResult> GetEstimatedProfitDashboard(
        Guid businessId,
        [FromQuery] int year,
        [FromQuery] int quarter)
    {
        var result = await _reportService
            .GetEstimatedProfitDashboardAsync(
                businessId,
                year,
                quarter);

        return Ok(
            ApiResponse<EstimatedProfitDashboardResponse>.Ok(
                result,
                "Get estimated profit dashboard successfully",
                HttpContext.TraceIdentifier));
    }
    
    [HttpGet("{businessId:guid}/active-quarters")]
    public async Task<IActionResult> GetActiveSalesQuarters(Guid businessId)
    {
        var result = await _reportService.GetActiveSalesQuartersAsync(
            businessId);

        return Ok(
            ApiResponse<List<ActiveSalesQuarterResponse>>.Ok(
                result,
                "Get active sales quarters successfully",
                HttpContext.TraceIdentifier));
    }
    
    [HttpGet("{businessId:guid}/cash-flow-dashboard")]
    public async Task<IActionResult> GetCashFlowDashboard(
        Guid businessId,
        [FromQuery] int year,
        [FromQuery] int quarter)
    {
        var result = await _reportService.GetCashFlowDashboardAsync(
            businessId,
            year,
            quarter);

        return Ok(
            ApiResponse<CashFlowDashboardResponse>.Ok(
                result,
                "Get cash flow dashboard successfully",
                HttpContext.TraceIdentifier));
    }
    
    [HttpGet("{businessId:guid}/tax-dashboard")]
    public async Task<IActionResult> GetTaxDashboard(
        Guid businessId,
        [FromQuery] int year)
    {
        var result = await _reportService.GetTaxDashboardAsync(
            businessId,
            year);

        return Ok(
            ApiResponse<TaxDashboardResponse>.Ok(
                result,
                "Get tax dashboard successfully",
                HttpContext.TraceIdentifier));
    }
}