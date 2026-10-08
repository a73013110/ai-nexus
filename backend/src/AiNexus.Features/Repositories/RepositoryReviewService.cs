using System.Text;
using System.Text.Json;
using AiNexus.Features.Inference;
using AiNexus.Features.Operations;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Repositories;

/// <summary>
/// Ownership, source checks and presentation shared by the review endpoints and <see cref="RepositoryReviewHandler"/>.
/// The job handler uses the throwing forms because a failed job records the error code of the exception.
/// </summary>
public sealed class RepositoryReviewService(NexusDbContext db, RepositoryService repositories, ModelPresentation presentation)
{
    public async Task<Result<RepositoryReview>> FindAsync(Guid owner, Guid id, CancellationToken ct)
        => await db.Set<RepositoryReview>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, ct) is { } row
            ? row : RepositoryErrors.ReviewNotFound;

    public async Task<RepositoryReview> OwnedAsync(Guid owner, Guid id, CancellationToken ct) => (await FindAsync(owner, id, ct)).OrThrow();

    /// <summary>The user can still read both pinned commits on the host the review was created on.</summary>
    public async Task<Result> CheckSourceAsync(Guid owner, RepositoryReview row, CancellationToken ct)
    {
        if (row.BaseUrl != repositories.BaseUrl) return RepositoryErrors.ReviewHostChanged;
        var head = await repositories.RequireCommitAsync(owner, row.Repository, row.Commit, ct);
        if (!head.IsSuccess || row.BaseCommit is null) return head;
        return await repositories.RequireCommitAsync(owner, row.Repository, row.BaseCommit, ct);
    }

    public async Task RequireSourceAsync(Guid owner, RepositoryReview row, CancellationToken ct) => (await CheckSourceAsync(owner, row, ct)).OrThrow();

    internal async Task<RepositoryReviewDto> DescribeAsync(RepositoryReview x, CancellationToken ct) =>
        Describe(x, await db.Set<BackgroundJob>().AsNoTracking().SingleAsync(j => j.Id == x.JobId, ct));

    internal RepositoryReviewDto Describe(RepositoryReview x, BackgroundJob job) => new(x.Id, x.Repository, x.Commit, x.BaseCommit,
        presentation.PublicId(x.ModelId), x.Note, x.CreatedAt, JobService.Describe(job), Snapshot(x).Purpose, presentation.DisplayName(x.ModelId));

    public static ReviewSnapshot Snapshot(RepositoryReview row) => JsonSerializer.Deserialize<ReviewSnapshot>(row.SnapshotJson) is { Version: 1 or 2 or 3 } snapshot
        ? snapshot : throw new ApiException(409, "review_snapshot_unsupported", "此 review 快照版本目前無法處理，請建立新的 review。");

    public static ReviewSlice[] Split(string diff, int budget)
    {
        if (string.IsNullOrWhiteSpace(diff)) throw new ApiException(400, "review_no_changes", "此 commit 或區間沒有可檢閱的變更。");
        if (Encoding.UTF8.GetByteCount(diff) > 256000) throw new ApiException(413, "repository_diff_limit", "變更超過 256 KB，請縮小區間。");
        var files = System.Text.RegularExpressions.Regex.Split(diff.Replace("\r\n", "\n"), "(?=^diff --git )", System.Text.RegularExpressions.RegexOptions.Multiline).Where(x => !string.IsNullOrWhiteSpace(x));
        var slices = new List<ReviewSlice>();
        foreach (var file in files)
        {
            var label = string.Concat(file.Split('\n')[0].Take(500));
            var binary = file.Contains("GIT binary patch", StringComparison.Ordinal) || file.Contains("Binary files ", StringComparison.Ordinal);
            var parts = RepositoryReviewPlan.Chunks(file, budget).ToArray();
            for (var i = 0; i < parts.Length; i++)
            {
                slices.Add(new(label + (parts.Length > 1 ? $" · 區段 {i + 1}" : ""), parts[i], binary));
                if (slices.Count > 60) throw new ApiException(413, "review_segment_limit", "變更需要超過 60 個區段，請縮小區間或選擇更大的模型。");
            }
        }
        return slices.ToArray();
    }
}
