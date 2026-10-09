using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Conversations;

[Comment("使用者私人對話、目前訊息分支、收藏封存與自訂指令。")]
public sealed class Conversation
{
    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Comment("資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。")]
    public Guid OwnerId { get; set; }
    [Comment("關聯專案的識別碼。")]
    public Guid? ProjectId { get; set; }
    [Comment("介面顯示標題。")]
    public string Title { get; set; } = "新對話";
    [Comment("對話目前顯示分支的最後訊息識別碼。")]
    public Guid? ActiveLeafId { get; set; }
    [Comment("資料建立時間，採 UTC offset。")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Comment("資料最後修改時間，採 UTC offset。")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Comment("是否邏輯刪除；不自動刪除歷史紀錄。")]
    public bool IsDeleted { get; set; }
    [Comment("是否標記收藏。")]
    public bool IsFavorite { get; set; }
    [Comment("是否封存對話；封存後不再接受新的生成。")]
    public bool IsArchived { get; set; }
    [Comment("對話專用的回答指令。")]
    public string SystemInstruction { get; set; } = "";
    public List<ConversationLabel> Labels { get; set; } = [];
}

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> conversation)
    {
        conversation.ToTable("Conversations", "conversations");
        conversation.HasKey(x => x.Id);
        conversation.Property(x => x.Title).HasMaxLength(ConversationQueries.TitleMaxLength);
        conversation.Property(x => x.SystemInstruction).HasMaxLength(ConversationQueries.InstructionMaxLength);
        conversation.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.UpdatedAt });
        conversation.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.IsArchived, x.IsFavorite, x.UpdatedAt });
        conversation.HasQueryFilter(SoftDelete.Filter, x => !x.IsDeleted);
    }
}
