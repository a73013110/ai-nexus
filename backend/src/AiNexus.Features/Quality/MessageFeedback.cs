using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Quality;

/// <summary>A user's private rating of one assistant answer in a conversation they own.</summary>
public sealed class MessageFeedback
{
    public Guid MessageId { get; set; }
    public Guid OwnerId { get; set; }
    public int Rating { get; set; }
    public string Reason { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed record FeedbackDto(Guid MessageId, Guid ConversationId, string ConversationTitle, int Rating, string Reason, string Note, DateTimeOffset UpdatedAt);

internal sealed class MessageFeedbackConfiguration : IEntityTypeConfiguration<MessageFeedback>
{
    public void Configure(EntityTypeBuilder<MessageFeedback> f)
    {
        f.ToTable("MessageFeedback", "quality"); f.HasKey(x => x.MessageId);
        f.Property(x => x.Reason).HasMaxLength(24); f.Property(x => x.Note).HasMaxLength(2000); f.HasIndex(x => new { x.OwnerId, x.UpdatedAt });
    }
}
