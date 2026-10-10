using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Billing;

// Prices are append-only versions. A charge copies the applicable tariff before dispatch.
[Comment("依供應商、模型、幣別及成本類型保存的不可變價格版本。")]
public sealed class ModelPrice
{
    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Comment("模型或搜尋服務供應商識別碼。")]
    public string Provider { get; set; } = "";
    [Comment("核准模型的內部識別碼。")]
    public string ModelId { get; set; } = "";
    [Comment("費用幣別代碼；不同幣別不可直接合計。")]
    public string Currency { get; set; } = "USD";
    [Comment("成本類型。")]
    public string Kind { get; set; } = "api";
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
    [Comment("使用者提供的補充說明。")]
    public string Note { get; set; } = "";
    [Comment("此價格版本開始生效的時間。")]
    public DateTimeOffset EffectiveAt { get; set; }
    [Comment("資料建立時間，採 UTC offset。")]
    public DateTimeOffset CreatedAt { get; set; }
    [Comment("建立紀錄的使用者識別碼。")]
    public Guid CreatedBy { get; set; }
}

internal sealed class ModelPriceConfiguration : IEntityTypeConfiguration<ModelPrice>
{
    public void Configure(EntityTypeBuilder<ModelPrice> price)
    {
        price.Property(x => x.EffectiveAt).HasValueGenerator<CreationTime>();
        price.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        price.ToTable("ModelPrices", "billing"); price.HasKey(x => x.Id);
        price.HasIndex(x => new { x.Provider, x.ModelId, x.EffectiveAt }).IsUnique();
        price.Property(x => x.Provider).HasMaxLength(32); price.Property(x => x.ModelId).HasMaxLength(160);
        price.Property(x => x.Currency).HasMaxLength(3); price.Property(x => x.Kind).HasMaxLength(16);
        price.Property(x => x.RequestCharge).HasMaxLength(16); price.Property(x => x.Note).HasMaxLength(500);
        price.Property(x => x.InputPerMillion).HasPrecision(20, 8); price.Property(x => x.CachedInputPerMillion).HasPrecision(20, 8);
        price.Property(x => x.OutputPerMillion).HasPrecision(20, 8); price.Property(x => x.PerRequest).HasPrecision(20, 8);
    }
}
