using System.Text.Json.Serialization;

namespace TaxMate.Model.DTO.Rag;

public class RagAskResponse
{
    public bool Success { get; set; }

    public string Question { get; set; } = null!;

    public string Answer { get; set; } = null!;

    public List<RagSourceResponse> Sources { get; set; } = [];
}

public class RagSourceResponse
{
    public string Document { get; set; } = null!;

    [JsonPropertyName("document_code")]
    public string DocumentCode { get; set; } = null!;

    public int? Dieu { get; set; }

    public object? Khoan { get; set; }

    public object? Diem { get; set; }

    public string? Title { get; set; }

    public double Score { get; set; }

    [JsonPropertyName("retrieval_source")]
    public string? RetrievalSource { get; set; }

    public int? Page { get; set; }

    public Dictionary<string, object>? Metadata { get; set; }
}