using AiNexus.Data.App.Identity;
using AiNexus.Data.App.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Quality;

/// <summary>對題組凍結的案例副本，比較多組模型變體的一次評測；由 <c>EvaluationHandler</c> 執行。</summary>
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
}

internal sealed class EvaluationRunConfiguration : IEntityTypeConfiguration<EvaluationRun>
{
    public void Configure(EntityTypeBuilder<EvaluationRun> r)
    {
        r.ToTable("EvaluationRuns", "quality");
        r.HasKey(x => x.Id);
        r.Property(x => x.SetTitle).HasMaxLength(120);
        r.HasOne<EvaluationSet>().WithMany().HasForeignKey(x => x.SetId).OnDelete(DeleteBehavior.Restrict);
        r.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        r.HasOne<BackgroundJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
    }
}
