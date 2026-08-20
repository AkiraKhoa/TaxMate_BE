using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.TaxPolicy;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/admin/tax-policy")]
[Authorize(Roles = UserRoles.Admin)]
public class TaxPolicyController : ControllerBase
{
    private readonly ITaxPolicyService _taxPolicyService;

    public TaxPolicyController(ITaxPolicyService taxPolicyService)
    {
        _taxPolicyService = taxPolicyService;
    }

    [HttpGet("{type}/latest")]
    public async Task<IActionResult> GetLatestThreshold(
        string type,
        CancellationToken cancellationToken)
    {
        var result = await _taxPolicyService.GetLatestThresholdAsync(
            type,
            cancellationToken);

        return Ok(ApiResponse<TaxThresholdSettingResponse>.Ok(
            result,
            "Get latest tax threshold successfully",
            HttpContext.TraceIdentifier));
    }

    [HttpGet("{type}")]
    public async Task<IActionResult> GetEffectiveThreshold(
        string type,
        [FromQuery] DateOnly effectiveOn,
        CancellationToken cancellationToken)
    {
        var result = await _taxPolicyService.GetEffectiveThresholdAsync(
            type,
            effectiveOn,
            cancellationToken);

        return Ok(ApiResponse<TaxThresholdSettingResponse>.Ok(
            result,
            "Get effective tax threshold successfully",
            HttpContext.TraceIdentifier));
    }

    [HttpPut("{type}")]
    public async Task<IActionResult> Upsert(
        string type,
        [FromBody] UpdateTaxThresholdSettingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _taxPolicyService.UpsertAsync(
            type,
            request,
            GetUserId(),
            cancellationToken);

        return Ok(ApiResponse<TaxThresholdSettingResponse>.Ok(
            result,
            "Tax threshold updated successfully",
            HttpContext.TraceIdentifier));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid user identity.");
    }
}
