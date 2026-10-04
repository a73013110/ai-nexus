namespace AiNexus.Modules.Conversations;

public sealed class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Title { get; set; } = "新對話";
    public Guid? ActiveLeafId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsArchived { get; set; }
    public string SystemInstruction { get; set; } = "";
    public List<ConversationLabel> Labels { get; set; } = [];
}

public sealed class ConversationLabel
{
    public Guid ConversationId { get; set; }
    public string Name { get; set; } = "";
}

public sealed class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public Guid? ParentId { get; set; }
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
    public string Status { get; set; } = "completed";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? RunId { get; set; }
    public string? ModelId { get; set; }
    public string? ErrorCode { get; set; }
}
