using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Repositories;

/// <summary>Cancels or retries the review's background job. Retry re-validates access and the source through <see cref="RepositoryReviewHandler"/>.</summary>
internal sealed class ChangeRepositoryReviewJob(RepositoryReviewService reviews, JobService jobs)
{
    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapPost("/reviews/{id:guid}/cancel", (Guid id, ICurrentUser user, ChangeRepositoryReviewJob handler, CancellationToken ct) =>
                handler.HandleAsync(user.Id, id, false, ct).ToHttpResultAsync())
            .WithName("CancelRepositoryReview");
        routes.MapPost("/reviews/{id:guid}/retry", (Guid id, ICurrentUser user, ChangeRepositoryReviewJob handler, CancellationToken ct) =>
                handler.HandleAsync(user.Id, id, true, ct).ToHttpResultAsync())
            .WithName("RetryRepositoryReview");
    }

    public async Task<Result<JobDto>> HandleAsync(Guid owner, Guid id, bool retry, CancellationToken ct)
    {
        var review = await reviews.FindAsync(owner, id, ct);
        if (!review.IsSuccess) return review.Error;
        var job = review.Value.JobId;
        return retry ? await jobs.RetryAsync(owner, job, ct) : await jobs.CancelAsync(owner, job, ct);
    }
}
