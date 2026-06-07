using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.DTO;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

/// <summary>Upload và quản lý văn bản pháp lý.</summary>
[Controller]
[Route("api/[controller]")]
public class LegalDocumentController : ControllerBase
{
    private readonly ILegalDocumentService _legalDocumentService;
    
    public LegalDocumentController(ILegalDocumentService legalDocumentService)
    {
        _legalDocumentService = legalDocumentService;
    }
    
    /// <summary>Upload văn bản pháp lý (multipart/form-data).</summary>
    /// <param name="request">Metadata và file văn bản.</param>
    [HttpPost]
    public async Task<IActionResult> Upload(
        [FromForm] UploadLegalDocumentRequest request)
    {
        var id = await _legalDocumentService.UploadAsync(request);

        return Ok(id);
    }
}
