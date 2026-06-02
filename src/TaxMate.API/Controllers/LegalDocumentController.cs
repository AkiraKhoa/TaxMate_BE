using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.DTO;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[Controller]
[Route("api/[controller]")]
public class LegalDocumentController : ControllerBase
{
    private readonly ILegalDocumentService _legalDocumentService;
    
    public LegalDocumentController(ILegalDocumentService legalDocumentService)
    {
        _legalDocumentService = legalDocumentService;
    }
    
    [HttpPost]
    public async Task<IActionResult> Upload(
        [FromForm] UploadLegalDocumentRequest request)
    {
        var id = await _legalDocumentService.UploadAsync(request);

        return Ok(id);
    }
}