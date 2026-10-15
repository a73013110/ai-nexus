using AiNexus.Features.Identity;
using AiNexus.Features.Jobs;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Repositories.Reviews;

/// <summary>The user's 50 most recent reviews of one repository on the current Gitea host.</summary>
internal sealed class ListRepositoryReviews(NexusDbContext db, RepositoryService gitea, RepositoryReviewService reviews)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder repository) => repository
        .MapGet("/reviews", (string owner, string name, ICurrentUser user, ListRepositoryReviews handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, owner + "/" + name, ct).ToHttpResultAsync())
        .WithName("ListRepositoryReviews");

    public async Task<Result<IReadOnlyList<RepositoryReviewDto>>> HandleAsync(Guid owner, string repo, CancellationToken ct)
    {
        if (!RepositoryService.IsRepository(repo)) return RepositoriesErrors.InvalidRepository;
        // Only the columns the summary shows: the frozen snapshot (up to a million characters per review) stays in the database.
        var rows = await db.Set<RepositoryReview>().AsNoTracking().Where(x => x.OwnerId == owner && x.BaseUrl == gitea.BaseUrl && x.Repository == repo)
            .OrderByDescending(x => x.CreatedAt).Take(50)
            .Join(db.Set<BackgroundJob>().AsNoTracking(), x => x.JobId, job => job.Id, (x, job) => new
            {
                Review = new RepositoryReview
                {
                    Id = x.Id, Repository = x.Repository, Commit = x.Commit, BaseCommit = x.BaseCommit, PullRequest = x.PullRequest, Purpose = x.Purpose,
                    ModelId = x.ModelId, Note = x.Note, CreatedAt = x.CreatedAt, CommentUrl = x.CommentUrl
                },
                Job = job
            }).ToListAsync(ct);
        return Result<IReadOnlyList<RepositoryReviewDto>>.Ok(rows.Select(x => reviews.Describe(x.Review, x.Job)).ToArray());
    }
}
