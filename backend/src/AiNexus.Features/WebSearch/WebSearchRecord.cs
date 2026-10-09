using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.WebSearch;

[Comment("使用者明確啟用的網路搜尋、冪等識別、結果與費用。")]
public sealed class WebSearchRecord
{
    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Comment("資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。")]
    public Guid OwnerId { get; set; }
    [Comment("關聯對話的識別碼。")]
    public Guid ConversationId { get; set; }
    [Comment("擁有者範圍內的冪等請求識別，避免重試重複處理。")]
    public string IdempotencyKey { get; set; } = "";
    [Comment("請求內容指紋，用於辨識冪等識別碼衝突。")]
    public string RequestHash { get; set; } = "";
    [Comment("業務執行狀態。")]
    public string Status { get; set; } = "running";
    [Comment("搜尋結果的 JSON 快照。")]
    public string ResultsJson { get; set; } = "[]";
    [Comment("資料建立時間，採 UTC offset。")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Comment("觸發搜尋的 GenerationRuns 識別碼。")]
    public Guid? RunId { get; set; }
}

internal sealed class WebSearchRecordConfiguration : IEntityTypeConfiguration<WebSearchRecord>
{
    public void Configure(EntityTypeBuilder<WebSearchRecord> item)
    {
        item.ToTable("WebSearches", "websearch"); item.HasKey(x => x.Id);
        item.Property(x => x.IdempotencyKey).HasMaxLength(80); item.Property(x => x.RequestHash).HasMaxLength(64);
        item.Property(x => x.Status).HasMaxLength(16); item.HasIndex(x => new { x.OwnerId, x.IdempotencyKey }).IsUnique();
        item.HasIndex(x => x.RunId); item.HasIndex(x => new { x.OwnerId, x.CreatedAt });
    }
}
