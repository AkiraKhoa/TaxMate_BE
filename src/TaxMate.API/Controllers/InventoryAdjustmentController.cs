using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/businesses/{businessId:guid}/inventory/adjustments")]
[Authorize(Roles = UserRoles.Owner)]
[Authorize(Policy = AuthPolicies.ActiveAccountOnly)]
public sealed class InventoryAdjustmentController : ControllerBase
{
    private readonly IInventoryAdjustmentService _service;

    public InventoryAdjustmentController(IInventoryAdjustmentService service)
    {
        _service = service;
    }

    /// <summary>
    /// Records a physical stocktake. The client submits actual quantities;
    /// movement directions and deltas are always derived by the backend.
    /// </summary>
    [HttpPost("reconcile")]
    public async Task<IActionResult> Reconcile(
        Guid businessId,
        [FromBody] ReconcileInventoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.ReconcileAsync(
            GetUserId(),
            businessId,
            request,
            enableStockTracking: false,
            cancellationToken);
        return Ok(ApiResponse<InventoryControlResultResponse>.Ok(
            result,
            "Inventory reconciled successfully",
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
