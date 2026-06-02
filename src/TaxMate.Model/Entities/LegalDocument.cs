using System.ComponentModel.DataAnnotations;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class LegalDocument : BaseEntity
{
    public Guid LegalDocumentId { get; set; }

    public string DocumentCode { get; set; } = null!;

    public string DocumentName { get; set; } = null!;

    public string? DocumentType { get; set; }

    public string? AuthorityLevel { get; set; }

    public DateTime? EffectiveDate { get; set; }

    public DateTime? ExpiredDate { get; set; }

    public string Status { get; set; } = "Active";

    // Storage

    public string SourceFileName { get; set; } = null!;

    public string StoragePath { get; set; } = null!;

    public long FileSize { get; set; }

    public string FileHash { get; set; } = null!;

    // RAG

    public bool IsIndexed { get; set; }

    public int? TotalPages { get; set; }

    public int? TotalChunks { get; set; }
}