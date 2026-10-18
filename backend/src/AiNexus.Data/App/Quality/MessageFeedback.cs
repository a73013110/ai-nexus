using AiNexus.Data.App.Conversations;
using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Quality;

/// <summary>使用者在自己擁有的對話中，對某則 AI 回答的私人評分。</summary>
public sealed class MessageFeedback
{
    public Guid MessageId { get; set; }
    public Guid OwnerId { get; set; }
    public int Rating { get; set; }
    public string Reason { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class MessageFeedbackConfiguration : IEntityTypeConfiguration<MessageFeedback>
{
    public void Configure(EntityTypeBuilder<MessageFeedback> f)
    {
        f.ToTable("MessageFeedback", "quality");
        f.HasKey(x => x.MessageId);
        f.Property(x => x.Reason).HasMaxLength(24);
        f.Property(x => x.Note).HasMaxLength(2000);
        f.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);
        f.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
