using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Quality.RetrievalEvaluations;

public sealed class RetrievalEvaluationResult
{
    public Guid RunId { get; set; }
    public int CaseIndex { get; set; }
    public string Mode { get; set; } = "";
    public string ActualMode { get; set; } = "";
    public string? Unavailable { get; set; }
    public double? Recall { get; set; }
    public double? ReciprocalRank { get; set; }
    public double? Ndcg { get; set; }
    public bool? Refused { get; set; }
    public long RewriteMs { get; set; }
    public long EmbedMs { get; set; }
    public long SearchMs { get; set; }
    public long RerankMs { get; set; }
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
