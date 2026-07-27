using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/businesses/{businessId:guid}/tax-books")]
// [Authorize]
public class TaxBookController : ControllerBase
{
    private readonly ITaxBookService _taxBookService;

    public TaxBookController(ITaxBookService taxBookService)
    {
        _taxBookService = taxBookService;
    }

    [HttpGet("s1a/export")]
    public async Task<IActionResult> ExportS1a(
        Guid businessId,
        [FromQuery] int year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        var result = await _taxBookService.ExportS1aAsync(
            GetUserId(),
            businessId,
            year,
            month,
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
