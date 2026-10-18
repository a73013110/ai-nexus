using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Conversations;

public sealed class ConversationLabel
{
    public Guid ConversationId { get; set; }
    public string Name { get; set; } = "";

    /// <summary>最多 5 個標籤，每個 1 到 24 字元，去除頭尾空白後彼此不可重複；清單不合法時回傳 null。</summary>
    public static string[]? Clean(IReadOnlyList<string> labels)
    {
        if (labels.Count > 5
            || labels.Any(x => string.IsNullOrWhiteSpace(x) || x.Trim().Length > 24 || x.Any(char.IsControl)))
        {
            return null;
        }
        return labels.Select(x => x.Trim()).Distinct(StringComparer.Ordinal).ToArray();
    }
}

internal sealed class ConversationLabelConfiguration : IEntityTypeConfiguration<ConversationLabel>
{
    public void Configure(EntityTypeBuilder<ConversationLabel> label)
    {
        label.ToTable("ConversationLabels", "conversations");
        label.HasKey(x => new { x.ConversationId, x.Name });
        label.Property(x => x.Name).HasMaxLength(24);
        label.HasOne<Conversation>()
            .WithMany(x => x.Labels)
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
