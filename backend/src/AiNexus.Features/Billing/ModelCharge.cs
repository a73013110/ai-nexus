using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Billing;

[Comment("各呼叫當時的價格與用量快照；未知費用保持空值。")]
public sealed class ModelCharge
{
    // Call ID is also the primary key: retries cannot create a second charge for a call.
    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; }
    [Comment("資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。")]
    public Guid OwnerId { get; set; }
    [Comment("關聯對話的識別碼。")]
    public Guid? ConversationId { get; set; }
    [Comment("模型或搜尋服務供應商識別碼。")]
    public string Provider { get; set; } = "";
    [Comment("核准模型的內部識別碼。")]
    public string ModelId { get; set; } = "";
    [Comment("模型呼叫或背景工作的操作類型。")]
    public string Operation { get; set; } = "chat";
    [Comment("資料建立時間，採 UTC offset。")]
    public DateTimeOffset CreatedAt { get; set; }
    [Comment("工作開始執行時間。")]
    public DateTimeOffset? StartedAt { get; set; }
    [Comment("工作結束時間。")]
    public DateTimeOffset? FinishedAt { get; set; }
    [Comment("此呼叫採用的不可變價格版本。")]
    public Guid? PriceId { get; set; }
    [Comment("費用幣別代碼；不同幣別不可直接合計。")]
    public string Currency { get; set; } = "";
    [Comment("成本類型。")]
    public string Kind { get; set; } = "unpriced";
    [Comment("每百萬輸入 tokens 的單價。")]
    public decimal InputPerMillion { get; set; }
    [Comment("每百萬快取輸入 tokens 的單價。")]
    public decimal CachedInputPerMillion { get; set; }
    [Comment("每百萬輸出 tokens 的單價。")]
    public decimal OutputPerMillion { get; set; }
    [Comment("每次呼叫的固定單價。")]
    public decimal PerRequest { get; set; }
    [Comment("搜尋服務每次請求的費用。")]
    public string RequestCharge { get; set; } = "completed";
    [Comment("模型回報的輸入 tokens；未知保持空值。")]
    public long? InputTokens { get; set; }
    [Comment("模型回報的快取輸入 tokens。")]
    public long? CachedInputTokens { get; set; }
    // Output includes the provider's billed reasoning tokens; these are a subset, not extra.
    [Comment("模型回報的輸出 tokens；未知保持空值。")]
    public long? OutputTokens { get; set; }
    [Comment("模型回報的推理 tokens。")]
    public long? ReasoningTokens { get; set; }
    [Comment("本次呼叫是否有完整且可計費的實際用量。")]
    public bool UsageComplete { get; set; }
    [Comment("此呼叫的已知費用；未知保持空值。")]
    public decimal? Amount { get; set; }
    [Comment("業務物件的生命週期狀態。")]
    public string State { get; set; } = "pending";
    [Comment("搜尋或處理操作的結果分類。")]
    public string Outcome { get; set; } = "queued";
}

internal sealed class ModelChargeConfiguration : IEntityTypeConfiguration<ModelCharge>
{
    public void Configure(EntityTypeBuilder<ModelCharge> charge)
    {
        charge.ToTable("ModelCharges", "billing"); charge.HasKey(x => x.Id);
        charge.HasIndex(x => new { x.OwnerId, x.CreatedAt }); charge.HasIndex(x => x.CreatedAt); charge.HasIndex(x => x.ConversationId);
        charge.Property(x => x.Provider).HasMaxLength(32); charge.Property(x => x.ModelId).HasMaxLength(160);
        charge.Property(x => x.Operation).HasMaxLength(32); charge.Property(x => x.Currency).HasMaxLength(3);
        charge.Property(x => x.Kind).HasMaxLength(16); charge.Property(x => x.RequestCharge).HasMaxLength(16);
        charge.Property(x => x.State).HasMaxLength(24); charge.Property(x => x.Outcome).HasMaxLength(16);
        charge.HasOne<ModelPrice>().WithMany().HasForeignKey(x => x.PriceId).OnDelete(DeleteBehavior.Restrict);
        charge.Property(x => x.InputPerMillion).HasPrecision(20, 8); charge.Property(x => x.CachedInputPerMillion).HasPrecision(20, 8);
        charge.Property(x => x.OutputPerMillion).HasPrecision(20, 8); charge.Property(x => x.PerRequest).HasPrecision(20, 8);
        charge.Property(x => x.Amount).HasPrecision(20, 8);
    }
}
