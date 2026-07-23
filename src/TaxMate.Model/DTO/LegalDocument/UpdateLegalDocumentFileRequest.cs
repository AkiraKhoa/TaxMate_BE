using Microsoft.AspNetCore.Http;

namespace TaxMate.Model.DTO.LegalDocument;

/// <summary>Thay thế file PDF của văn bản pháp lý (multipart/form-data).</summary>
public class UpdateLegalDocumentFileRequest
{
    public IFormFile File { get; set; } = null!;
}
