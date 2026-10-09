using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Conversations;

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

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> conversation)
    {
        conversation.ToTable("Conversations", "conversations");
        conversation.HasKey(x => x.Id);
        conversation.Property(x => x.Title).HasMaxLength(ConversationQueries.TitleMaxLength);
        conversation.Property(x => x.SystemInstruction).HasMaxLength(ConversationQueries.InstructionMaxLength);
        conversation.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.UpdatedAt });
        conversation.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.IsArchived, x.IsFavorite, x.UpdatedAt });
        conversation.HasQueryFilter(SoftDelete.Filter, x => !x.IsDeleted);
    }
}
