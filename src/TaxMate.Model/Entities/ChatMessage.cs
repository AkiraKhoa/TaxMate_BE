namespace TaxMate.Model.Entities;

public class ChatMessage
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public string Role { get; set; } = null!;

    public string Content { get; set; } = null!;

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int TotalTokens { get; set; }

    public string? ModelName { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ChatConversation Conversation { get; set; } = null!;

    public virtual ICollection<ChatReference> References { get; set; }
        = new List<ChatReference>();
}