using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Conversations;

public sealed class ConversationLabel
{
    public Guid ConversationId { get; set; }
    public string Name { get; set; } = "";
}

internal sealed class ConversationLabelConfiguration : IEntityTypeConfiguration<ConversationLabel>
{
    public void Configure(EntityTypeBuilder<ConversationLabel> label)
    {
        label.ToTable("ConversationLabels", "conversations");
        label.HasKey(x => new { x.ConversationId, x.Name });
        label.Property(x => x.Name).HasMaxLength(24);
        label.HasOne<Conversation>().WithMany(x => x.Labels).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}
