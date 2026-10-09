using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Quality.Evaluations;

[Comment("各案例／模型組合的輸出、指標、用量與人工覆核結果。")]
public sealed class EvaluationResult
{
    [Comment("關聯 EvaluationRuns 的識別碼。")]
    public Guid RunId { get; set; }
    [Comment("評測案例的從零開始索引。")]
    public int CaseIndex { get; set; }
    [Comment("評測模型／參數組合的從零開始索引。")]
    public int VariantIndex { get; set; }
    [Comment("評測模型的實際回答。")]
    public string Output { get; set; } = "";
    [Comment("評測輸出是否因上限截斷。")]
    public bool Truncated { get; set; }
    [Comment("命中的必要條件數。")]
    public int RequiredMatches { get; set; }
    [Comment("必要條件總數。")]
    public int RequiredTotal { get; set; }
    [Comment("命中的禁止條件數。")]
    public int ForbiddenMatches { get; set; }
    [Comment("執行耗時，以毫秒計。")]
    public long ElapsedMs { get; set; }
    [Comment("模型回報的輸入 tokens；未知保持空值。")]
    public long? InputTokens { get; set; }
    [Comment("模型回報的輸出 tokens；未知保持空值。")]
    public long? OutputTokens { get; set; }
    [Comment("人工覆核分數；未覆核保持空值。")]
    public int? ReviewScore { get; set; }
    [Comment("人工覆核意見。")]
    public string ReviewNote { get; set; } = "";
    [Comment("人工覆核者的使用者識別碼。")]
    public Guid? ReviewerId { get; set; }
}

internal sealed class EvaluationResultConfiguration : IEntityTypeConfiguration<EvaluationResult>
{
    public void Configure(EntityTypeBuilder<EvaluationResult> v)
    {
        v.ToTable("EvaluationResults", "quality"); v.HasKey(x => new { x.RunId, x.CaseIndex, x.VariantIndex }); v.Property(x => x.ReviewNote).HasMaxLength(2000);
        v.HasOne<EvaluationRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}
