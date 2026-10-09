using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Quality.Evaluations;

public sealed class EvaluationResult
{
    public Guid RunId { get; set; }
    public int CaseIndex { get; set; }
    public int VariantIndex { get; set; }
    public string Output { get; set; } = "";
    public bool Truncated { get; set; }
    public int RequiredMatches { get; set; }
    public int RequiredTotal { get; set; }
    public int ForbiddenMatches { get; set; }
    public long ElapsedMs { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public int? ReviewScore { get; set; }
    public string ReviewNote { get; set; } = "";
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
