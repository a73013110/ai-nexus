using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Conversations;

[Comment("使用者對話的分類標籤。")]
public sealed class ConversationLabel
{
    [Comment("關聯對話的識別碼。")]
    public Guid ConversationId { get; set; }
    [Comment("業務物件的顯示名稱。")]
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
