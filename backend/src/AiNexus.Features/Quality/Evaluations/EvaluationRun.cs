using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Quality.Evaluations;

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

internal sealed class EvaluationRunConfiguration : IEntityTypeConfiguration<EvaluationRun>
{
    public void Configure(EntityTypeBuilder<EvaluationRun> r)
    {
        r.ToTable("EvaluationRuns", "quality"); r.HasKey(x => x.Id); r.Property(x => x.SetTitle).HasMaxLength(120); r.HasIndex(x => new { x.SetId, x.CreatedAt }); r.HasIndex(x => x.JobId).IsUnique();
        r.HasOne<EvaluationSet>().WithMany().HasForeignKey(x => x.SetId).OnDelete(DeleteBehavior.Restrict);
    }
}
