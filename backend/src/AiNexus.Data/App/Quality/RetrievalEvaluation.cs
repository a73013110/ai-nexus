using AiNexus.Data.App.Identity;
using AiNexus.Data.App.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Quality;

/// <summary>在擁有者的知識庫上執行的一次檢索驗收，由 <c>RetrievalEvaluationHandler</c> 執行。</summary>
public sealed class RetrievalEvaluation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid JobId { get; set; }
    public string Title { get; set; } = "";
    public string CollectionsJson { get; set; } = "[]";
    public string CasesJson { get; set; } = "[]";
    public string ConfigurationFingerprint { get; set; } = "";
    public string ProfileKey { get; set; } = "";
    public int TopK { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class RetrievalEvaluationConfiguration : IEntityTypeConfiguration<RetrievalEvaluation>
{
    public void Configure(EntityTypeBuilder<RetrievalEvaluation> r)
    {
        r.ToTable("RetrievalEvaluations", "quality");
        r.HasKey(x => x.Id);
        r.Property(x => x.Title).HasMaxLength(120);
        r.Property(x => x.ProfileKey).HasMaxLength(200);
        r.Property(x => x.ConfigurationFingerprint).HasMaxLength(64);
        r.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        r.HasOne<BackgroundJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
    }
}
