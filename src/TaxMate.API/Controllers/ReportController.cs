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
[Authorize(Roles = UserRoles.Owner)]
[Authorize(Policy = AuthPolicies.ActiveAccountOnly)]
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly IS2aHkdExportService _s2aHkdExportService;

    public ReportController(
        IReportService reportService,
        IS2aHkdExportService s2aHkdExportService)
    {
        _reportService = reportService;
        _s2aHkdExportService = s2aHkdExportService;
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

    [HttpGet("{businessId:guid}/s2a-hkd/preview")]
    public async Task<IActionResult> GetS2aHkdPreview(
        Guid businessId,
        [FromQuery] int year,
        [FromQuery] int quarter)
    {
        var result = await _s2aHkdExportService.BuildDocumentModelAsync(
            GetUserId(),
            businessId,
            year,
            quarter);

        return Ok(
            ApiResponse<IReadOnlyList<S2aHkdDocumentModel>>.Ok(
                result,
                "Get S2a-HKD preview successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("{businessId:guid}/s2a-hkd")]
    public async Task<IActionResult> ExportS2aHkd(
        Guid businessId,
        [FromQuery] int year,
        [FromQuery] int quarter)
    {
        var bytes = await _s2aHkdExportService.ExportDocxAsync(
            GetUserId(),
            businessId,
            year,
            quarter);

        var fileName = $"S2a-HKD_Q{quarter}_{year}.docx";
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            fileName);
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Invalid user token.");

        return userId;
    }
}