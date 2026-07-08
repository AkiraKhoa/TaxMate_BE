namespace TaxMate.Model.Entities;

public class ChatReference
{
    public Guid Id { get; set; }

    public Guid MessageId { get; set; }

    public Guid LegalDocumentId { get; set; }

    public string ChunkId { get; set; } = null!;

    public double SimilarityScore { get; set; }

    public virtual ChatMessage Message { get; set; } = null!;

    public virtual LegalDocument LegalDocument { get; set; } = null!;
}