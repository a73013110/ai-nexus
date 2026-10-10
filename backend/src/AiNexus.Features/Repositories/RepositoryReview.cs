using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Repositories;

/// <summary>A review whose source and model configuration are frozen before its durable job is enqueued.</summary>
[Comment("固定 commit 或區間 diff 的私人 review、模型設定指紋與背景任務。")]
public sealed class RepositoryReview
{
    public const int NoteMaxLength = 2000;

    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Comment("資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。")]
    public Guid OwnerId { get; set; }
    [Comment("關聯背景工作識別碼。")]
    public Guid JobId { get; set; }
    [Comment("Gitea 連線主機位址。")]
    public string BaseUrl { get; set; } = "";
    [Comment("Gitea repository 的 owner/name 識別。")]
    public string Repository { get; set; } = "";
    [Comment("匯入當時固定的 commit SHA。")]
    public string Commit { get; set; } = "";
    [Comment("區間 review 的起點 commit SHA；空值表示單一 commit。")]
    public string? BaseCommit { get; set; }
    [Comment("核准模型的內部識別碼。")]
    public string ModelId { get; set; } = "";
    [Comment("固定模型與生成設定的 SHA-256 指紋。")]
    public string ConfigurationFingerprint { get; set; } = "";
    [Comment("使用者提供的補充說明。")]
    public string Note { get; set; } = "";
    [Comment("擁有者範圍內的冪等請求識別，避免重試重複處理。")]
    public string IdempotencyKey { get; set; } = "";
    [Comment("請求內容指紋，用於辨識冪等識別碼衝突。")]
    public string RequestHash { get; set; } = "";
    [Comment("分享時的固定內容快照；不隨後續編輯變動。")]
    public string SnapshotJson { get; set; } = "";
    [Comment("資料建立時間，採 UTC offset。")]
    public DateTimeOffset CreatedAt { get; set; }
}
[Comment("Review 各區段的持久結果及用量；重試沿用已完成區段。")]
public sealed class RepositoryReviewResult
{
    [Comment("關聯私人程式碼 review 的識別碼。")]
    public Guid ReviewId { get; set; }
    [Comment("同一父物件內的呈現順序。")]
    public int Ordinal { get; set; }
    [Comment("評測模型的實際回答。")]
    public string Output { get; set; } = "";
    [Comment("評測輸出是否因上限截斷。")]
    public bool Truncated { get; set; }
    [Comment("模型回報的輸入 tokens；未知保持空值。")]
    public long? InputTokens { get; set; }
    [Comment("模型回報的輸出 tokens；未知保持空值。")]
    public long? OutputTokens { get; set; }
    [Comment("執行耗時，以毫秒計。")]
    public long ElapsedMs { get; set; }
}
public sealed record RepositoryReviewDto(Guid Id, string Repository, string Commit, string? BaseCommit, string ModelId, string Note, DateTimeOffset CreatedAt, JobDto Job, string Purpose = "review", string? ModelDisplayName = null);
public sealed record RepositoryReviewSectionDto(int Ordinal, string Label, string Diff, bool Binary, string? Output, bool Truncated, long? InputTokens, long? OutputTokens, long? ElapsedMs);
public sealed record RepositoryReviewReportDto(string Output, bool Truncated, long? InputTokens, long? OutputTokens, long ElapsedMs);
public sealed record RepositoryReviewDetailDto(RepositoryReviewDto Review, IReadOnlyList<RepositoryReviewSectionDto> Sections, RepositoryReviewReportDto? Report = null, int Version = 1);

internal sealed class RepositoryReviewConfiguration : IEntityTypeConfiguration<RepositoryReview>
{
    public void Configure(EntityTypeBuilder<RepositoryReview> r)
    {
        r.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        r.ToTable("RepositoryReviews", "repositories"); r.HasKey(x => x.Id);
        r.Property(x => x.BaseUrl).HasMaxLength(500); r.Property(x => x.Repository).HasMaxLength(201); r.Property(x => x.Commit).HasMaxLength(64); r.Property(x => x.BaseCommit).HasMaxLength(64);
        r.Property(x => x.ModelId).HasMaxLength(160); r.Property(x => x.ConfigurationFingerprint).HasMaxLength(64); r.Property(x => x.Note).HasMaxLength(RepositoryReview.NoteMaxLength);
        r.Property(x => x.IdempotencyKey).HasMaxLength(80); r.Property(x => x.RequestHash).HasMaxLength(64); r.Property(x => x.SnapshotJson).HasMaxLength(1000000);
        r.HasIndex(x => new { x.OwnerId, x.IdempotencyKey }).IsUnique(); r.HasIndex(x => new { x.OwnerId, x.CreatedAt });
    }
}

internal sealed class RepositoryReviewResultConfiguration : IEntityTypeConfiguration<RepositoryReviewResult>
{
    public void Configure(EntityTypeBuilder<RepositoryReviewResult> s)
    {
        s.ToTable("RepositoryReviewResults", "repositories"); s.HasKey(x => new { x.ReviewId, x.Ordinal });
        s.Property(x => x.Output).HasMaxLength(64000); s.HasOne<RepositoryReview>().WithMany().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Restrict);
    }
}
