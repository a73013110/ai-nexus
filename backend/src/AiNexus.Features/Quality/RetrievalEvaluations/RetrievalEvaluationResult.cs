using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Quality.RetrievalEvaluations;

[Comment("四種檢索模式的相關性、無來源拒答與延遲指標；不保存查詢或檢索來源原文。")]
public sealed class RetrievalEvaluationResult
{
    [Comment("關聯 RetrievalEvaluations 的識別碼。")]
    public Guid RunId { get; set; }
    [Comment("評測案例的從零開始索引。")]
    public int CaseIndex { get; set; }
    [Comment("請求的檢索比較模式。")]
    public string Mode { get; set; } = "";
    [Comment("實際檢索模式，包含略過或降級標記。")]
    public string ActualMode { get; set; } = "";
    [Comment("此模式無法評測的錯誤代碼；空值表示已完成。")]
    public string? Unavailable { get; set; }
    [Comment("前 K 筆命中的相關文件比例；無答案題保持空值。")]
    public double? Recall { get; set; }
    [Comment("第一筆相關命中的排名倒數；無答案題保持空值。")]
    public double? ReciprocalRank { get; set; }
    [Comment("前 K 筆依相關性分級計算的正規化折損累積增益。")]
    public double? Ndcg { get; set; }
    [Comment("無答案題是否未提供來源；有答案題保持空值。")]
    public bool? Refused { get; set; }
    [Comment("查詢改寫耗時，單位毫秒。")]
    public long RewriteMs { get; set; }
    [Comment("查詢向量化含快取耗時，單位毫秒。")]
    public long EmbedMs { get; set; }
    [Comment("授權候選召回耗時，單位毫秒。")]
    public long SearchMs { get; set; }
    [Comment("重排耗時，單位毫秒。")]
    public long RerankMs { get; set; }
    [Comment("執行耗時，以毫秒計。")]
    public long ElapsedMs { get; set; }
}

internal sealed class RetrievalEvaluationResultConfiguration : IEntityTypeConfiguration<RetrievalEvaluationResult>
{
    public void Configure(EntityTypeBuilder<RetrievalEvaluationResult> v)
    {
        v.ToTable("RetrievalEvaluationResults", "quality"); v.HasKey(x => new { x.RunId, x.CaseIndex, x.Mode });
        v.Property(x => x.Mode).HasMaxLength(24); v.Property(x => x.ActualMode).HasMaxLength(120); v.Property(x => x.Unavailable).HasMaxLength(80);
        v.HasOne<RetrievalEvaluation>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}
