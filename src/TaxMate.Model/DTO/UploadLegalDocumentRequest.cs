using Microsoft.AspNetCore.Http;

namespace TaxMate.Model.DTO;

/// <summary>Yêu cầu upload văn bản pháp lý (multipart/form-data).</summary>
public class UploadLegalDocumentRequest
{
    /// <summary>Mã văn bản (duy nhất).</summary>
    /// <example>VB-2024-001</example>
    public string DocumentCode { get; set; } = null!;

    /// <summary>Tên văn bản.</summary>
    /// <example>Thông tư hướng dẫn thuế GTGT</example>
    public string DocumentName { get; set; } = null!;

    /// <summary>Loại văn bản.</summary>
    /// <example>Thông tư</example>
    public string? DocumentType { get; set; }

    /// <summary>Cấp ban hành.</summary>
    /// <example>Trung ương</example>
    public string? AuthorityLevel { get; set; }

    /// <summary>Ngày hiệu lực.</summary>
    /// <example>2024-01-01</example>
    public DateTime? EffectiveDate { get; set; }

    /// <summary>File PDF/DOC upload (chọn file thủ công trên Swagger).</summary>
    public IFormFile File { get; set; } = null!;
}
