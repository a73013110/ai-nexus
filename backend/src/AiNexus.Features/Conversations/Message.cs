using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Conversations;

[Comment("對話訊息樹；提問、回答、重新生成與編輯保留各版本。")]
public sealed class Message
{
    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Comment("關聯對話的識別碼。")]
    public Guid ConversationId { get; set; }
    [Comment("父訊息識別碼；編輯與重新生成形成分支樹。")]
    public Guid? ParentId { get; set; }
    [Comment("訊息角色：user 或 assistant。")]
    public string Role { get; set; } = "user";
    [Comment("訊息文字內容。")]
    public string Content { get; set; } = "";
    [Comment("業務執行狀態。")]
    public string Status { get; set; } = "completed";
    [Comment("資料建立時間，採 UTC offset。")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Comment("產生此訊息的 GenerationRuns 識別碼。")]
    public Guid? RunId { get; set; }
    [Comment("核准模型的內部識別碼。")]
    public string? ModelId { get; set; }
    [Comment("伺服器產生的不透明問題查證代碼；每個問題個別識別。")]
    public string? IssueCode { get; set; }
    [Comment("對外安全的錯誤代碼，不含密碼或完整例外。")]
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
