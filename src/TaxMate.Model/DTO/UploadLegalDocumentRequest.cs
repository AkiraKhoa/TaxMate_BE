using Microsoft.AspNetCore.Http;

namespace TaxMate.Model.DTO;

public class UploadLegalDocumentRequest
{
    public string DocumentCode { get; set; } = null!;

    public string DocumentName { get; set; } = null!;

    public string? DocumentType { get; set; }

    public string? AuthorityLevel { get; set; }

    public DateTime? EffectiveDate { get; set; }

    public IFormFile File { get; set; } = null!;
}