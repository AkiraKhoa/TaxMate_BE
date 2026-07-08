using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class ChatConversation
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid? BusinessId { get; set; }

    public string Title { get; set; } = null!;

    public string Status { get; set; } = ChatConversationStatus.Active;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual BusinessProfile? Business { get; set; }

    public virtual ICollection<ChatMessage> Messages { get; set; }
        = new List<ChatMessage>();
}