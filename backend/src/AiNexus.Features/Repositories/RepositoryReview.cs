using AiNexus.Features.Identity;
using AiNexus.Features.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Repositories;

/// <summary>A review whose source and model configuration are frozen before its durable job is enqueued.</summary>
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
    public string ModelId { get; set; } = "";
    public string ConfigurationFingerprint { get; set; } = "";
    public string Note { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string SnapshotJson { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class RepositoryReviewResult
{
    public Guid ReviewId { get; set; }
    public int Ordinal { get; set; }
    public string Output { get; set; } = "";
    public bool Truncated { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long ElapsedMs { get; set; }
}
public sealed record RepositoryReviewDto(Guid Id, string Repository, string Commit, string? BaseCommit, string ModelId, string Note, DateTimeOffset CreatedAt, JobDto Job, string Purpose = "review", string? ModelDisplayName = null);
public sealed record RepositoryReviewSectionDto(int Ordinal, string Label, string Diff, bool Binary, string? Output, bool Truncated, long? InputTokens, long? OutputTokens, long? ElapsedMs);
public sealed record RepositoryReviewReportDto(string Output, bool Truncated, long? InputTokens, long? OutputTokens, long ElapsedMs);
public sealed record RepositoryReviewDetailDto(RepositoryReviewDto Review, IReadOnlyList<RepositoryReviewSectionDto> Sections, RepositoryReviewReportDto? Report = null, int Version = 1);

/// <summary>Entry point kept for <c>NexusDbContext</c>.</summary>
public static class RepositoryReviewConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.ApplyConfiguration(new RepositoryReviewEntityConfiguration());
        model.ApplyConfiguration(new RepositoryReviewResultConfiguration());
    }
}

internal sealed class RepositoryReviewEntityConfiguration : IEntityTypeConfiguration<RepositoryReview>
{
    public void Configure(EntityTypeBuilder<RepositoryReview> r)
    {
        r.ToTable("RepositoryReviews", "workspace"); r.HasKey(x => x.Id);
        r.Property(x => x.BaseUrl).HasMaxLength(500); r.Property(x => x.Repository).HasMaxLength(201); r.Property(x => x.Commit).HasMaxLength(64); r.Property(x => x.BaseCommit).HasMaxLength(64);
        r.Property(x => x.ModelId).HasMaxLength(160); r.Property(x => x.ConfigurationFingerprint).HasMaxLength(64); r.Property(x => x.Note).HasMaxLength(RepositoryReview.NoteMaxLength);
        r.Property(x => x.IdempotencyKey).HasMaxLength(80); r.Property(x => x.RequestHash).HasMaxLength(64); r.Property(x => x.SnapshotJson).HasMaxLength(1000000);
        r.HasIndex(x => new { x.OwnerId, x.IdempotencyKey }).IsUnique(); r.HasIndex(x => new { x.OwnerId, x.CreatedAt });
        r.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        r.HasOne<BackgroundJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RepositoryReviewResultConfiguration : IEntityTypeConfiguration<RepositoryReviewResult>
{
    public void Configure(EntityTypeBuilder<RepositoryReviewResult> s)
    {
        s.ToTable("RepositoryReviewResults", "workspace"); s.HasKey(x => new { x.ReviewId, x.Ordinal });
        s.Property(x => x.Output).HasMaxLength(64000); s.HasOne<RepositoryReview>().WithMany().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Restrict);
    }
}
