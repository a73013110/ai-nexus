using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Repositories;

/// <summary>The user's 30 most recent reviews on the current Gitea host, optionally for one repository.</summary>
internal sealed class ListRepositoryReviews(NexusDbContext db, RepositoryService gitea, RepositoryReviewService reviews)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/reviews", async (string? repository, ICurrentUser user, ListRepositoryReviews handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, repository, ct)).ToHttpResult())
        .WithName("ListRepositoryReviews").Produces<IReadOnlyList<RepositoryReviewDto>>();

    public async Task<Result<IReadOnlyList<RepositoryReviewDto>>> HandleAsync(Guid owner, string? repository, CancellationToken ct)
    {
        if (repository is not null && !RepositoryService.IsRepository(repository)) return RepositoryErrors.InvalidRepository;
        var rows = await db.Set<RepositoryReview>().AsNoTracking().Where(x => x.OwnerId == owner && x.BaseUrl == gitea.BaseUrl && (repository == null || x.Repository == repository))
            .OrderByDescending(x => x.CreatedAt).Take(30)
            .Join(db.Set<BackgroundJob>().AsNoTracking(), x => x.JobId, job => job.Id, (review, job) => new { Review = review, Job = job }).ToListAsync(ct);
        return Result<IReadOnlyList<RepositoryReviewDto>>.Ok(rows.Select(x => reviews.Describe(x.Review, x.Job)).ToArray());
    }
}
