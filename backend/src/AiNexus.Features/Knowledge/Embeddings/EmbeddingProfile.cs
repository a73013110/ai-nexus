using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Knowledge.Embeddings;

[Comment("向量空間及切段規則的不可變快照；同時最多一個 active。")]
public sealed class EmbeddingProfile
{
    [Comment("資料的主鍵識別碼。")]
    public int Id { get; set; }
    [Comment("供應商、模型、維度及輸入／切段規則的唯一指紋。")]
    public string Key { get; set; } = "";
    [Comment("模型或搜尋服務供應商識別碼。")]
    public string Provider { get; set; } = "";
    [Comment("建立此向量空間時的模型識別碼。")]
    public string Model { get; set; } = "";
    [Comment("向量維度，限已建立資料表的 allowlist。")]
    public int Dimensions { get; set; }
    [Comment("查詢輸入格式 plain 或 qwen-query。")]
    public string InputFormat { get; set; } = "plain";
    [Comment("qwen-query 的檢索任務指令快照。")]
    public string QueryInstruction { get; set; } = "";
    [Comment("外部來源或 repository 的固定版本識別。")]
    public string Revision { get; set; } = "";
    [Comment("切段器版本及 token／重疊參數快照；重建期間保留舊版本。")]
    public string ChunkerConfiguration { get; set; } = "";
    [Comment("業務執行狀態。")]
    public string Status { get; set; } = "building";
    [Comment("資料建立時間，採 UTC offset。")]
    public DateTimeOffset CreatedAt { get; set; }
    [Comment("此 profile 完整覆蓋並切換為 active 的 UTC 時間。")]
    public DateTimeOffset? ActivatedAt { get; set; }
    [Comment("此 profile 退役的 UTC 時間；作為保留期清理依據。")]
    public DateTimeOffset? RetiredAt { get; set; }
}

internal sealed class EmbeddingProfileConfiguration : IEntityTypeConfiguration<EmbeddingProfile>
{
    public void Configure(EntityTypeBuilder<EmbeddingProfile> profile)
    {
        profile.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        profile.ToTable("EmbeddingProfiles", "knowledge"); profile.HasKey(x => x.Id);
        profile.Property(x => x.Key).HasMaxLength(200); profile.HasIndex(x => x.Key).IsUnique();
        profile.Property(x => x.Provider).HasMaxLength(32); profile.Property(x => x.Model).HasMaxLength(160); profile.Property(x => x.InputFormat).HasMaxLength(32);
        profile.Property(x => x.QueryInstruction).HasMaxLength(500); profile.Property(x => x.Revision).HasMaxLength(64); profile.Property(x => x.ChunkerConfiguration).HasMaxLength(500);
        profile.Property(x => x.Status).HasMaxLength(16); profile.HasIndex(x => x.Status).IsUnique().HasFilter("[Status] = 'active'");
    }
}
