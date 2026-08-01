namespace TaxMate.Model.DTO.LegalDocument;

public class LegalDocumentResponse
{
    public Guid LegalDocumentId { get; set; }

    public string DocumentCode { get; set; } = null!;

    public string DocumentName { get; set; } = null!;

    public string? DocumentType { get; set; }

    public string? AuthorityLevel { get; set; }

    public DateTime? EffectiveDate { get; set; }

    public DateTime? ExpiredDate { get; set; }

    public string Status { get; set; } = null!;

    public string SourceFileName { get; set; } = null!;

    public string StoragePath { get; set; } = null!;

    public long FileSize { get; set; }

    public string FileHash { get; set; } = null!;

    public bool IsIndexed { get; set; }

    public int? TotalPages { get; set; }

    public int? TotalChunks { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
