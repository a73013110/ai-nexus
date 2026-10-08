using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Repositories;

/// <summary>A review with its source sections and checkpointed results, readable only while the user can still read the source.</summary>
internal sealed class ReadRepositoryReview(NexusDbContext db, RepositoryReviewService reviews)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/reviews/{id:guid}", async (Guid id, ICurrentUser user, ReadRepositoryReview handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, ct)).ToHttpResult())
        .WithName("ReadRepositoryReview").Produces<RepositoryReviewDetailDto>();

    public async Task<Result<RepositoryReviewDetailDto>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var found = await reviews.FindAsync(owner, id, ct);
        if (!found.IsSuccess) return found.Error;
        var row = found.Value;
        var source = await reviews.CheckSourceAsync(owner, row, ct);
        if (!source.IsSuccess) return source.Error;
        var results = await db.Set<RepositoryReviewResult>().AsNoTracking().Where(x => x.ReviewId == id).ToDictionaryAsync(x => x.Ordinal, ct);
        var snapshot = RepositoryReviewService.Snapshot(row);
        results.TryGetValue(RepositoryReviewPlan.ReportOrdinal, out var report);
        return new RepositoryReviewDetailDto(await reviews.DescribeAsync(row, ct), snapshot.Slices.Select((slice, i) => {
            results.TryGetValue(i, out var result);
            return new RepositoryReviewSectionDto(i, slice.Label, slice.Diff, slice.Binary, result?.Output, result?.Truncated ?? false, result?.InputTokens, result?.OutputTokens, result?.ElapsedMs);
        }).ToArray(), report is null ? null : new(report.Output, report.Truncated, report.InputTokens, report.OutputTokens, report.ElapsedMs), snapshot.Version);
    }
}
