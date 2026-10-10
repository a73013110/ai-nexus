using System.Text;
using System.Text.Json;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Repositories;

/// <summary>
/// Ownership, source checks and presentation shared by the review endpoints and <see cref="RepositoryReviewHandler"/>.
/// </summary>
public sealed class RepositoryReviewService(NexusDbContext db, RepositoryService repositories, ModelPresentation presentation)
{
    public async Task<Result<RepositoryReview>> FindAsync(Guid owner, Guid id, CancellationToken ct)
        => await db.Set<RepositoryReview>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, ct) is { } row
            ? row : RepositoriesErrors.ReviewNotFound;

    /// <summary>The user can still read both pinned commits on the host the review was created on.</summary>
    public async Task<Result> CheckSourceAsync(Guid owner, RepositoryReview row, CancellationToken ct)
    {
        if (row.BaseUrl != repositories.BaseUrl) return RepositoriesErrors.ReviewHostChanged;
        var head = await repositories.RequireCommitAsync(owner, row.Repository, row.Commit, ct);
        if (!head.IsSuccess || row.BaseCommit is null) return head;
        return await repositories.RequireCommitAsync(owner, row.Repository, row.BaseCommit, ct);
    }

    internal async Task<RepositoryReviewDto> DescribeAsync(RepositoryReview x, CancellationToken ct) =>
        Describe(x, await db.Set<BackgroundJob>().AsNoTracking().SingleAsync(j => j.Id == x.JobId, ct));

    internal RepositoryReviewDto Describe(RepositoryReview x, BackgroundJob job) => new(x.Id, x.Repository, x.Commit, x.BaseCommit,
        presentation.PublicId(x.ModelId), x.Note, x.CreatedAt, JobService.Describe(job), Snapshot(x) is { IsSuccess: true } snapshot ? snapshot.Value.Purpose : "review", presentation.DisplayName(x.ModelId));

    public static Result<ReviewSnapshot> Snapshot(RepositoryReview row) => JsonSerializer.Deserialize<ReviewSnapshot>(row.SnapshotJson) is { Version: 1 or 2 or 3 } snapshot
        ? snapshot : RepositoriesErrors.SnapshotUnsupported;

    public static Result<ReviewSlice[]> Split(string diff, int budget)
    {
        if (string.IsNullOrWhiteSpace(diff)) return RepositoriesErrors.ReviewNoChanges;
        if (Encoding.UTF8.GetByteCount(diff) > 256000) return RepositoriesErrors.DiffLimit;
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
                if (slices.Count > 60) return RepositoriesErrors.SegmentLimit;
            }
        }
        return slices.ToArray();
    }
}
