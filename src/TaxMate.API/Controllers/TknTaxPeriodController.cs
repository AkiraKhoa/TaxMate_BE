using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.TaxPeriod;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/tkn-tax-periods")]
public sealed class TknTaxPeriodController : ControllerBase
{
    private readonly ITknTaxPeriodService _service;
    public TknTaxPeriodController(ITknTaxPeriodService service) => _service = service;

    [HttpGet("{taxPeriodId:guid}/preview")]
    public async Task<IActionResult> Preview(Guid taxPeriodId, CancellationToken token)
        => Ok(ApiResponse<TknTaxPeriodPreviewResponse>.Ok(
            await _service.GetPreviewAsync(GetUserId(), taxPeriodId, token),
            "TKN period preview retrieved successfully.", HttpContext.TraceIdentifier));

    [HttpPost("{taxPeriodId:guid}/close")]
    public async Task<IActionResult> Close(Guid taxPeriodId,
        [FromBody] CloseTknTaxPeriodRequest request, CancellationToken token)
        => Ok(ApiResponse<CloseTknTaxPeriodResponse>.Ok(
            await _service.CloseAsync(GetUserId(), taxPeriodId, request, token),
            "TKN period closed successfully.", HttpContext.TraceIdentifier));

    [HttpPost("{taxPeriodId:guid}/calculate")]
    public async Task<IActionResult> Calculate(Guid taxPeriodId, CancellationToken token)
        => Ok(ApiResponse<TknTaxCalculationResponse>.Ok(
            await _service.CalculateAsync(GetUserId(), taxPeriodId, token),
            "TKN period calculated successfully.", HttpContext.TraceIdentifier));

    [HttpGet("{taxPeriodId:guid}/qtt-next-step")]
    public async Task<IActionResult> GetQttNextStep(
        Guid taxPeriodId,
        CancellationToken token)
        => Ok(ApiResponse<TknQttNextStepResponse>.Ok(
            await _service.GetQttNextStepAsync(GetUserId(), taxPeriodId, token),
            "TKN QTT next step retrieved successfully.",
            HttpContext.TraceIdentifier));

    [HttpPost("{taxPeriodId:guid}/qtt-next-step")]
    public async Task<IActionResult> ApplyQttNextStep(
        Guid taxPeriodId,
        [FromBody] ApplyTknQttNextStepRequest request,
        CancellationToken token)
        => Ok(ApiResponse<TknQttNextStepResponse>.Ok(
            await _service.ApplyQttNextStepAsync(
                GetUserId(), taxPeriodId, request, token),
            "TKN QTT next step applied successfully.",
            HttpContext.TraceIdentifier));

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var id)
            ? id
            : throw new UnauthorizedAccessException("Token invalid.");
    }
}
