using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Quality.RetrievalEvaluations;

/// <summary>A retrieval acceptance run over the owner's collections, executed by <see cref="RetrievalEvaluationHandler"/>.</summary>
[Comment("檢索驗收集、授權知識庫、索引與設定指紋及可續跑背景工作；不保存檢索來源原文。")]
public sealed class RetrievalEvaluation
{
    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Comment("資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。")]
    public Guid OwnerId { get; set; }
    [Comment("關聯背景工作識別碼。")]
    public Guid JobId { get; set; }
    [Comment("介面顯示標題。")]
    public string Title { get; set; } = "";
    [Comment("評測所使用的知識庫識別碼陣列；執行與讀取時重新檢查授權。")]
    public string CollectionsJson { get; set; } = "[]";
    [Comment("固定評測案例的 JSON 快照。")]
    public string CasesJson { get; set; } = "[]";
    [Comment("固定模型與生成設定的 SHA-256 指紋。")]
    public string ConfigurationFingerprint { get; set; } = "";
    [Comment("評測所固定的向量空間及切段規則識別。")]
    public string ProfileKey { get; set; } = "";
    [Comment("評測所固定的最大檢索結果數。")]
    public int TopK { get; set; }
    [Comment("資料建立時間，採 UTC offset。")]
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class RetrievalEvaluationConfiguration : IEntityTypeConfiguration<RetrievalEvaluation>
{
    public void Configure(EntityTypeBuilder<RetrievalEvaluation> r)
    {
        r.ToTable("RetrievalEvaluations", "quality"); r.HasKey(x => x.Id);
        r.Property(x => x.Title).HasMaxLength(120); r.Property(x => x.ProfileKey).HasMaxLength(200); r.Property(x => x.ConfigurationFingerprint).HasMaxLength(64);
        r.HasIndex(x => new { x.OwnerId, x.CreatedAt }); r.HasIndex(x => x.JobId).IsUnique();
    }
}
