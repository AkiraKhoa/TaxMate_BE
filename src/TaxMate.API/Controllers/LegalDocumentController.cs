using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.LegalDocument;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

/// <summary>Upload và quản lý văn bản pháp lý (Admin).</summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
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

        return Created(
            $"api/LegalDocument/{id}",
            ApiResponse<Guid>.Ok(
                id,
                "Upload legal document successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Danh sách tất cả văn bản pháp lý.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAllLegalDocuments()
    {
        return Ok(
            ApiResponse<List<LegalDocumentResponse>>.Ok(
                await _legalDocumentService.GetAllAsync(),
                "Get legal documents successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Lấy chi tiết văn bản pháp lý theo ID.</summary>
    /// <param name="id">ID văn bản pháp lý.</param>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var document =
            await _legalDocumentService.GetByIdAsync(id);

        return Ok(
            ApiResponse<LegalDocumentResponse>.Ok(
                document,
                "Get legal document successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Vô hiệu hóa văn bản pháp lý.</summary>
    /// <param name="id">ID văn bản pháp lý.</param>
    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await _legalDocumentService.DeactivateAsync(id);

        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Document deactivated successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Kích hoạt lại văn bản pháp lý.</summary>
    /// <param name="id">ID văn bản pháp lý.</param>
    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        await _legalDocumentService.ActivateAsync(id);

        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Document activated successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Danh sách văn bản pháp lý đang hoạt động.</summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetAllActiveLegalDocuments()
    {
        return Ok(
            ApiResponse<List<LegalDocumentResponse>>.Ok(
                await _legalDocumentService.GetActiveAsync(),
                "Get active legal documents successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Cập nhật (thay thế) file PDF của văn bản pháp lý.</summary>
    [HttpPut("{id:guid}/file")]
    public async Task<IActionResult> UpdateFile(
        Guid id,
        [FromForm] UpdateLegalDocumentFileRequest request)
    {
        var document = await _legalDocumentService.UpdateFileAsync(id, request);

        return Ok(
            ApiResponse<LegalDocumentResponse>.Ok(
                document,
                "Update legal document file successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Xóa văn bản pháp lý.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _legalDocumentService.DeleteAsync(id);

        return Ok(
            ApiResponse<object?>.Ok(
                null,
                "Delete legal document successfully",
                HttpContext.TraceIdentifier));
    }
}
