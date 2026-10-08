using AiNexus.Features.Identity;
using AiNexus.Features.Operations;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Repositories;

/// <summary>Cancels or retries the review's background job. Retry re-validates access and the source through <see cref="RepositoryReviewHandler"/>.</summary>
internal static class ChangeRepositoryReviewJob
{
    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapPost("/reviews/{id:guid}/cancel", (Guid id, ICurrentUser user, RepositoryReviewService reviews, JobService jobs, CancellationToken ct) =>
                HandleAsync(user.Id, id, false, reviews, jobs, ct))
            .WithName("CancelRepositoryReview").Produces<JobDto>();
        routes.MapPost("/reviews/{id:guid}/retry", (Guid id, ICurrentUser user, RepositoryReviewService reviews, JobService jobs, CancellationToken ct) =>
                HandleAsync(user.Id, id, true, reviews, jobs, ct))
            .WithName("RetryRepositoryReview").Produces<JobDto>();
    }

    private static async Task<IResult> HandleAsync(Guid owner, Guid id, bool retry, RepositoryReviewService reviews, JobService jobs, CancellationToken ct)
    {
        var review = await reviews.FindAsync(owner, id, ct);
        if (!review.IsSuccess) return review.Error.ToProblem();
        var job = review.Value.JobId;
        return TypedResults.Ok(retry ? await jobs.RetryAsync(owner, job, ct) : await jobs.CancelAsync(owner, job, ct));
    }
}
