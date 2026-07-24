using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.TaxPeriod;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/tax-periods")]
// [Authorize(Roles = UserRoles.Owner)]
// [Authorize(Policy = AuthPolicies.ActiveAccountOnly)]
public class TaxPeriodController : ControllerBase
{
    private readonly ITaxPeriodService _taxPeriodService;

    public TaxPeriodController(
        ITaxPeriodService taxPeriodService)
    {
        _taxPeriodService = taxPeriodService;
    }

    [HttpGet("business/{businessId:guid}")]
    public async Task<IActionResult> GetByBusiness(
        Guid businessId,
        [FromQuery] GetTaxPeriodsRequest request,
        CancellationToken cancellationToken)
    {
        var result =
            await _taxPeriodService.GetByBusinessAsync(
                GetUserId(),
                businessId,
                request,
                cancellationToken);

        return Ok(
            ApiResponse<IReadOnlyList<TaxPeriodSummaryResponse>>.Ok(
                result,
                "Tax periods retrieved successfully.",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("{taxPeriodId:guid}")]
    public async Task<IActionResult> GetById(
        Guid taxPeriodId,
        CancellationToken cancellationToken)
    {
        var result = await _taxPeriodService.GetByIdAsync(
            GetUserId(),
            taxPeriodId,
            cancellationToken);

        return Ok(
            ApiResponse<TaxPeriodDetailResponse>.Ok(
                result,
                "Tax period retrieved successfully.",
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
    
    [HttpGet("{taxPeriodId:guid}/preview")]
    public async Task<IActionResult> GetPreview(
        Guid taxPeriodId,
        CancellationToken cancellationToken)
    {
        var result =
            await _taxPeriodService.GetPreviewAsync(
                GetUserId(),
                taxPeriodId,
                cancellationToken);

        return Ok(
            ApiResponse<TaxPeriodPreviewResponse>.Ok(
                result,
                "Tax period preview retrieved successfully.",
                HttpContext.TraceIdentifier));
    }
    
    [HttpPost("{taxPeriodId:guid}/close")]
    public async Task<IActionResult> Close(
        Guid taxPeriodId,
        [FromBody] CloseTaxPeriodRequest request,
        CancellationToken cancellationToken)
    {
        var result =
            await _taxPeriodService.CloseAsync(
                GetUserId(),
                taxPeriodId,
                request,
                cancellationToken);

        return Ok(
            ApiResponse<CloseTaxPeriodResponse>.Ok(
                result,
                "Tax period closed successfully.",
                HttpContext.TraceIdentifier));
    }
    
    [HttpPost("{taxPeriodId:guid}/calculate")]
    public async Task<IActionResult> Calculate(
        Guid taxPeriodId,
        CancellationToken cancellationToken)
    {
        var result =
            await _taxPeriodService.CalculateAsync(
                GetUserId(),
                taxPeriodId,
                cancellationToken);

        return Ok(
            ApiResponse<TaxCalculationResponse>.Ok(
                result,
                "Tax calculated successfully.",
                HttpContext.TraceIdentifier));
    }
}