using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.Rag;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/rag")]
public class RagController : ControllerBase
{
    private readonly IRagClient _ragClient;

    public RagController(IRagClient ragClient)
    {
        _ragClient = ragClient;
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask(
        [FromBody] RagAskRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _ragClient.AskAsync(
            request,
            cancellationToken);

        return Ok(
            ApiResponse<RagAskResponse>.Ok(
                result,
                "Ask TaxMate RAG successfully",
                HttpContext.TraceIdentifier));
    }
}