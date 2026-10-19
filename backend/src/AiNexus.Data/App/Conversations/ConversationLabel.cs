using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Conversations;

public sealed class ConversationLabel
{
    public const int NameMaxLength = 24;
    public const int MaxPerConversation = 5;

    public Guid ConversationId { get; set; }
    public string Name { get; set; } = "";

    /// <summary>最多 <see cref="MaxPerConversation"/> 個標籤，每個 1 到 <see cref="NameMaxLength"/> 字元，去除頭尾空白後彼此不可重複；清單不合法時回傳 null。</summary>
    public static string[]? Clean(IReadOnlyList<string> labels)
    {
        if (labels.Count > MaxPerConversation
            || labels.Any(x => string.IsNullOrWhiteSpace(x) || x.Trim().Length > NameMaxLength || x.Any(char.IsControl)))
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
        label.Property(x => x.Name).HasMaxLength(ConversationLabel.NameMaxLength);
        label.HasOne<Conversation>()
            .WithMany(x => x.Labels)
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
