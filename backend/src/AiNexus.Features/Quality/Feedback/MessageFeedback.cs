using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Quality.Feedback;

/// <summary>A user's private rating of one assistant answer in a conversation they own.</summary>
[Comment("使用者對 AI 回答的私人評分與意見。")]
public sealed class MessageFeedback
{
    [Comment("關聯訊息的識別碼。")]
    public Guid MessageId { get; set; }
    [Comment("資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。")]
    public Guid OwnerId { get; set; }
    [Comment("使用者對回答的評分。")]
    public int Rating { get; set; }
    [Comment("回饋理由。")]
    public string Reason { get; set; } = "";
    [Comment("使用者提供的補充說明。")]
    public string Note { get; set; } = "";
    [Comment("資料最後修改時間，採 UTC offset。")]
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class MessageFeedbackConfiguration : IEntityTypeConfiguration<MessageFeedback>
{
    public void Configure(EntityTypeBuilder<MessageFeedback> f)
    {
        f.ToTable("MessageFeedback", "quality"); f.HasKey(x => x.MessageId);
        f.Property(x => x.Reason).HasMaxLength(24); f.Property(x => x.Note).HasMaxLength(2000); f.HasIndex(x => new { x.OwnerId, x.UpdatedAt });
    }
}
