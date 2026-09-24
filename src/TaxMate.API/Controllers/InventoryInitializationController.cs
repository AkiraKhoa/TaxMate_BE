using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/businesses/{businessId:guid}/inventory/initialization")]
[Authorize(Roles = UserRoles.Owner)]
[Authorize(Policy = AuthPolicies.ActiveAccountOnly)]
public sealed class InventoryInitializationController : ControllerBase
{
    private readonly IInventoryInitializationService _service;

    public InventoryInitializationController(IInventoryInitializationService service)
    {
        _service = service;
    }

    [HttpGet("preview")]
    public async Task<IActionResult> GetPreview(
        Guid businessId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetPreviewAsync(
            GetUserId(),
            businessId,
            cancellationToken);
        return Ok(ApiResponse<InventoryInitializationPreviewResponse>.Ok(
            result,
            "Get inventory initialization preview successfully",
            HttpContext.TraceIdentifier));
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(
        Guid businessId,
        [FromBody] InitializeInventoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.InitializeAsync(
            GetUserId(),
            businessId,
            request,
            cancellationToken);
        return Ok(ApiResponse<InventoryControlResultResponse>.Ok(
            result,
            "Inventory opening balances initialized successfully",
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
