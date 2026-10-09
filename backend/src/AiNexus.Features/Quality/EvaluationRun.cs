using AiNexus.Features.Inference;
using AiNexus.Features.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Quality;

/// <summary>One comparison of model variants over a frozen copy of a set's cases, executed by <see cref="EvaluationHandler"/>.</summary>
public sealed class EvaluationRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SetId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid JobId { get; set; }
    public int SetVersion { get; set; }
    public string SetTitle { get; set; } = "";
    public string CasesJson { get; set; } = "[]";
    public string VariantsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }

    public EvaluationRunDto ToDto(BackgroundJob job, Guid actor) => new(Id, SetId, SetTitle, SetVersion, OwnerId == actor, CreatedAt, JobService.Describe(job));
}

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

public sealed record EvaluationVariant(string Label, string ModelId, string Instruction, ModelTaskSnapshot? Configuration = null, string? ModelDisplayName = null);
public sealed record EvaluationRunDto(Guid Id, Guid SetId, string Title, int SetVersion, bool CanControl, DateTimeOffset CreatedAt, JobDto Job);
public sealed record EvaluationResultDto(int CaseIndex, int VariantIndex, string Output, bool Truncated, int RequiredMatches, int RequiredTotal, int ForbiddenMatches, long ElapsedMs, long? InputTokens, long? OutputTokens, int? ReviewScore, string ReviewNote);
public sealed record EvaluationDetailDto(EvaluationRunDto Run, IReadOnlyList<EvaluationCase> Cases, IReadOnlyList<EvaluationVariant> Variants, IReadOnlyList<EvaluationResultDto> Results, bool CanReview);

internal sealed class EvaluationRunConfiguration : IEntityTypeConfiguration<EvaluationRun>
{
    public void Configure(EntityTypeBuilder<EvaluationRun> r)
    {
        r.ToTable("EvaluationRuns", "quality"); r.HasKey(x => x.Id); r.Property(x => x.SetTitle).HasMaxLength(120); r.HasIndex(x => new { x.SetId, x.CreatedAt }); r.HasIndex(x => x.JobId).IsUnique();
        r.HasOne<EvaluationSet>().WithMany().HasForeignKey(x => x.SetId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EvaluationResultConfiguration : IEntityTypeConfiguration<EvaluationResult>
{
    public void Configure(EntityTypeBuilder<EvaluationResult> v)
    {
        v.ToTable("EvaluationResults", "quality"); v.HasKey(x => new { x.RunId, x.CaseIndex, x.VariantIndex }); v.Property(x => x.ReviewNote).HasMaxLength(2000);
        v.HasOne<EvaluationRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}
