using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.TaxDeclaration;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/tax-declarations")]
// [Authorize(Roles = UserRoles.Owner)]
// [Authorize(Policy = AuthPolicies.ActiveAccountOnly)]
public class TaxDeclarationController : ControllerBase
{
    private readonly ITaxDeclarationService _service;

    public TaxDeclarationController(
        ITaxDeclarationService service)
    {
        _service = service;
    }

    [HttpPost("tax-period/{taxPeriodId:guid}")]
    public async Task<IActionResult> Create(
        Guid taxPeriodId,
        [FromBody] CreateTaxDeclarationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(
            GetUserId(),
            taxPeriodId,
            request,
            cancellationToken);

        return Ok(
            ApiResponse<TaxDeclarationResponse>.Ok(
                result,
                "Tax declaration created successfully.",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("{declarationId:guid}")]
    public async Task<IActionResult> GetById(
        Guid declarationId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(
            GetUserId(),
            declarationId,
            cancellationToken);

        return Ok(
            ApiResponse<TaxDeclarationResponse>.Ok(
                result,
                "Tax declaration retrieved successfully.",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("tax-period/{taxPeriodId:guid}")]
    public async Task<IActionResult> GetByTaxPeriod(
        Guid taxPeriodId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByTaxPeriodAsync(
            GetUserId(),
            taxPeriodId,
            cancellationToken);

        return Ok(
            ApiResponse<TaxDeclarationResponse>.Ok(
                result,
                "Tax declaration retrieved successfully.",
                HttpContext.TraceIdentifier));
    }

    [HttpPost("{declarationId:guid}/submit")]
    public async Task<IActionResult> Submit(
        Guid declarationId,
        [FromBody] SubmitTaxDeclarationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.SubmitAsync(
            GetUserId(),
            declarationId,
            request,
            cancellationToken);

        return Ok(
            ApiResponse<TaxDeclarationResponse>.Ok(
                result,
                "Tax declaration submitted successfully.",
                HttpContext.TraceIdentifier));
    }
    
    [HttpGet("{declarationId:guid}/export")]
    public async Task<IActionResult> Export(
        Guid declarationId,
        CancellationToken cancellationToken)
    {
        var result =
            await _service.ExportAsync(
                GetUserId(),
                declarationId,
                cancellationToken);

        return File(
            result.Content,
            result.ContentType,
            result.FileName);
    }

    private Guid GetUserId()
    {
        var rawUserId = User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(rawUserId, out var userId))
        {
            throw new UnauthorizedAccessException(
                "Invalid user identifier.");
        }

        return userId;
    }
}