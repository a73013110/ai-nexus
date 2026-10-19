using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Repositories;
using System.Text.Json;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Features.Work.Jobs;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Repositories.Reviews;

/// <summary>review 端點與 <see cref="RepositoryReviewHandler"/> 共用的擁有權檢查、來源檢查與呈現。</summary>
public sealed class RepositoryReviewService(AppDbContext db, RepositoryService repositories, ModelPresentation presentation)
{
    public async Task<Result<RepositoryReview>> FindAsync(Guid owner, Guid id, CancellationToken ct)
        => await db.Set<RepositoryReview>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, ct) is { } row
            ? row : RepositoriesErrors.ReviewNotFound;

    /// <summary>使用者在 review 建立時所在的主機上，仍能讀取兩個固定的 commit。</summary>
    public async Task<Result> CheckSourceAsync(Guid owner, RepositoryReview row, CancellationToken ct)
    {
        if (row.BaseUrl != repositories.BaseUrl) return RepositoriesErrors.ReviewHostChanged;
        var head = await repositories.RequireCommitAsync(owner, row.Repository, row.Commit, ct);
        if (!head.IsSuccess || row.BaseCommit is null) return head;
        return await repositories.RequireCommitAsync(owner, row.Repository, row.BaseCommit, ct);
    }

    internal async Task<RepositoryReviewDto> DescribeAsync(RepositoryReview x, CancellationToken ct) =>
        Describe(x, await db.Set<BackgroundJob>().AsNoTracking().SingleAsync(j => j.Id == x.JobId, ct));

    /// <summary>review 的摘要；只讀取欄位，不讀 <see cref="RepositoryReview.SnapshotJson"/>。</summary>
    internal RepositoryReviewDto Describe(RepositoryReview x, BackgroundJob job) => new(x.Id, x.Repository, x.Commit, x.BaseCommit, x.PullRequest,
        x.Purpose, presentation.PublicId(x.ModelId), presentation.DisplayName(x.ModelId) ?? presentation.PublicId(x.ModelId),
        x.Note, x.CreatedAt, JobService.Describe(job), x.CommentUrl);

    public static Result<ReviewSnapshot> Snapshot(RepositoryReview row) => JsonSerializer.Deserialize<ReviewSnapshot>(row.SnapshotJson) is { Version: ReviewSnapshot.Current } snapshot
        ? snapshot : RepositoriesErrors.SnapshotUnsupported;
}
