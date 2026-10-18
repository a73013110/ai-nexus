using AiNexus.Data.App.Identity;
using AiNexus.Data.App.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Repositories;

/// <summary>在持久化背景工作排入佇列之前，就已凍結來源與模型設定的 review。</summary>
public sealed class RepositoryReview
{
    public const int NoteMaxLength = 2000;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid JobId { get; set; }
    public string BaseUrl { get; set; } = "";
    public string Repository { get; set; } = "";
    public string Commit { get; set; } = "";
    public string? BaseCommit { get; set; }
    public int? PullRequest { get; set; }
    public string ModelId { get; set; } = "";
    public string ConfigurationFingerprint { get; set; } = "";
    public string Note { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string SnapshotJson { get; set; } = "";
    public string Purpose { get; set; } = "review";
    public DateTimeOffset CreatedAt { get; set; }
    public string? CommentUrl { get; set; }
    public DateTimeOffset? CommentedAt { get; set; }
}

internal sealed class RepositoryReviewConfiguration : IEntityTypeConfiguration<RepositoryReview>
{
    public void Configure(EntityTypeBuilder<RepositoryReview> r)
    {
        r.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        r.ToTable("RepositoryReviews", "repositories");
        r.HasKey(x => x.Id);
        r.Property(x => x.BaseUrl).HasMaxLength(500);
        r.Property(x => x.Repository).HasMaxLength(201);
        r.Property(x => x.Commit).HasMaxLength(64);
        r.Property(x => x.BaseCommit).HasMaxLength(64);
        r.Property(x => x.ModelId).HasMaxLength(160);
        r.Property(x => x.ConfigurationFingerprint).HasMaxLength(64);
        r.Property(x => x.Note).HasMaxLength(RepositoryReview.NoteMaxLength);
        r.Property(x => x.CommentUrl).HasMaxLength(500);
        r.Property(x => x.IdempotencyKey).HasMaxLength(80);
        r.Property(x => x.RequestHash).HasMaxLength(64);
        r.Property(x => x.SnapshotJson).HasMaxLength(1000000);
        r.Property(x => x.Purpose).HasMaxLength(16);
        r.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        r.HasOne<BackgroundJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
    }
}
