using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Repositories.Reviews;

/// <summary>review 與其來源區段及已存的各區段結果；只有使用者仍能讀取來源時才可讀取。</summary>
internal sealed class ReadRepositoryReview(AppDbContext db, RepositoryReviewService reviews) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/reviews/{id:guid}", (Guid id, ICurrentUser user, ReadRepositoryReview handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct))
        .Audited(RepositoriesAudit.ReviewRead)
        .WithName("ReadRepositoryReview");

    public async Task<Result<RepositoryReviewDetailDto>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var found = await reviews.FindAsync(owner, id, ct);
        if (!found.IsSuccess) return found.Error;
        var row = found.Value;
        var source = await reviews.CheckSourceAsync(owner, row, ct);
        if (!source.IsSuccess) return source.Error;
        var results = await db.RepositoryReviewResults.AsNoTracking().Where(x => x.ReviewId == id).ToDictionaryAsync(x => x.Ordinal, ct);
        var frozen = RepositoryReviewService.Snapshot(row);
        if (!frozen.IsSuccess) return frozen.Error;
        var snapshot = frozen.Value;
        var description = await reviews.DescribeAsync(row, ct);
        var sections = snapshot.Slices.Select((slice, i) =>
        {
            results.TryGetValue(i, out var result);
            return new RepositoryReviewSectionDto(i, slice.Label, slice.Diff, slice.Binary, result?.Output, result?.Truncated ?? false,
                result?.InputTokens, result?.OutputTokens, result?.ElapsedMs);
        }).ToArray();
        results.TryGetValue(RepositoryReviewPlan.ReportOrdinal, out var report);
        RepositoryReviewReportDto? reportDto = report is null
            ? null
            : new(report.Output, report.Truncated, report.InputTokens, report.OutputTokens, report.ElapsedMs);
        return new RepositoryReviewDetailDto(description, sections, reportDto);
    }
}
