using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Conversations;

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
    public string? IssueCode { get; set; }
    public string? ErrorCode { get; set; }
}

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> message)
    {
        message.ToTable("Messages", "conversations");
        message.HasKey(x => x.Id);
        message.Property(x => x.Role).HasMaxLength(16);
        message.Property(x => x.Status).HasMaxLength(16);
        message.Property(x => x.ModelId).HasMaxLength(160);
        message.Property(x => x.ErrorCode).HasMaxLength(80); message.Property(x => x.IssueCode).HasMaxLength(40);
        message.HasIndex(x => new { x.ConversationId, x.CreatedAt });
        message.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
        message.HasOne<Message>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}
