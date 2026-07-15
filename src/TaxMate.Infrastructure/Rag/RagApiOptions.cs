namespace TaxMate.Infrastructure.Rag;

public class RagApiOptions
{
    public const string SectionName = "RagApi";

    public string BaseUrl { get; set; } = null!;

    public string AskEndpoint { get; set; } = "/api/v1/rag/ask";

    public int TimeoutSeconds { get; set; } = 120;
}