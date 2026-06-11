namespace TaxMate.Model.DTO.LegalDocument;

public class LegalDocumentResponse
{
    public Guid LegalDocumentId { get; set; }

    public string DocumentCode { get; set; }

    public string DocumentName { get; set; }

    public string? DocumentType { get; set; }

    public string Status { get; set; }

    public bool IsIndexed { get; set; }

    public DateTime CreatedAt { get; set; }
}