using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.LegalDocument;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
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

        return Created(
            $"api/LegalDocument/{id}",
            ApiResponse<Guid>.Ok(
                id,
                "Upload legal document successfully",
                HttpContext.TraceIdentifier));
    }
    
    [HttpGet]
    public async Task<IActionResult> GetAllLegalDocuments()
    {
        return Ok(
            ApiResponse<List<LegalDocumentResponse>>.Ok(
                await _legalDocumentService.GetAllAsync(),
                "Get legal documents successfully",
                HttpContext.TraceIdentifier));
    }
    
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id)
    {
        var document =
            await _legalDocumentService
                .GetByIdAsync(id);

        return Ok(
            ApiResponse<LegalDocumentResponse>.Ok(
                document,
                "Get legal document successfully",
                HttpContext.TraceIdentifier));
    }
    
    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid id)
    {
        await _legalDocumentService
            .DeactivateAsync(id);

        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Document deactivated successfully",
                HttpContext.TraceIdentifier));
    }
    
    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(
        Guid id)
    {
        await _legalDocumentService
            .ActivateAsync(id);

        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Document activated successfully",
                HttpContext.TraceIdentifier));
    }
    
    [HttpGet("active")]
    public async Task<IActionResult> GetAllActiveLegalDocuments()
    {
        return Ok(
            ApiResponse<List<LegalDocumentResponse>>.Ok(
                await _legalDocumentService.GetActiveAsync(),
                "Get active legal documents successfully",
                HttpContext.TraceIdentifier));
    }
}