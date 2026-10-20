using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Mail;

/// <summary>入列時渲染好的內文；<see cref="Protected"/> 時兩欄都是 Data Protection 密文。純附屬資料，隨郵件刪除。</summary>
public sealed class OutboundMessageContent
{
    public Guid MessageId { get; set; }
    public bool Protected { get; set; }
    public string? Html { get; set; }
    public string Text { get; set; } = "";
}

internal sealed class OutboundMessageContentConfiguration : IEntityTypeConfiguration<OutboundMessageContent>
{
    public void Configure(EntityTypeBuilder<OutboundMessageContent> content)
    {
        content.ToTable("OutboundMessageContents", "mail");
        content.HasKey(x => x.MessageId);
        content.HasOne<OutboundMessage>().WithOne().HasForeignKey<OutboundMessageContent>(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}
