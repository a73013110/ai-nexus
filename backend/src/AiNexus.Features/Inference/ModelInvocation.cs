using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Inference;

/// <summary>One non-chat model call (OCR, text transformation, evaluation, review) for quota and usage accounting.</summary>
[Comment("文字、OCR、embedding 等模型呼叫的狀態與實際用量。")]
public sealed class ModelInvocation
{
    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Comment("資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。")]
    public Guid OwnerId { get; set; }
    [Comment("模型呼叫種類。")]
    public string Kind { get; set; } = "";
    [Comment("核准模型的內部識別碼。")]
    public string ModelId { get; set; } = "";
    [Comment("模型或搜尋服務供應商識別碼。")]
    public string Provider { get; set; } = "google";
    [Comment("從請求建立至終止的總耗時毫秒；包括排隊、生成、取消與失敗。")]
    public long? DurationMilliseconds { get; set; }
    [Comment("業務執行狀態。")]
    public string Status { get; set; } = "running";
    [Comment("生成預留的保守輸入加最大輸出 token；執行中或缺失 usage 時占用配額，未執行即取消釋放。")]
    public long ReservedTokens { get; set; }
    [Comment("模型回報的輸入 tokens；未知保持空值。")]
    public long? InputTokens { get; set; }
    [Comment("模型回報的輸出 tokens；未知保持空值。")]
    public long? OutputTokens { get; set; }
    [Comment("資料建立時間，採 UTC offset。")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class ModelInvocationConfiguration : IEntityTypeConfiguration<ModelInvocation>
{
    public void Configure(EntityTypeBuilder<ModelInvocation> item)
    {
        item.ToTable("ModelInvocations", "inference"); item.HasKey(x => x.Id);
        item.Property(x => x.Kind).HasMaxLength(32); item.Property(x => x.ModelId).HasMaxLength(160); item.Property(x => x.Status).HasMaxLength(16);
        item.Property(x => x.Provider).HasMaxLength(32);
        item.HasIndex(x => new { x.OwnerId, x.CreatedAt });
    }
}
