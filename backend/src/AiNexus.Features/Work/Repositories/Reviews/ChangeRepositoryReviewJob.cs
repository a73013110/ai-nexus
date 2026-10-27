using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Features.Work.Jobs;

namespace AiNexus.Features.Work.Repositories.Reviews;

/// <summary>取消或重試 review 的背景工作。重試時會經由 <see cref="RepositoryReviewHandler"/> 重新驗證權限與來源。</summary>
internal sealed class ChangeRepositoryReviewJob(RepositoryReviewService reviews, JobService jobs)
{
    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapPost("/reviews/{id:guid}/cancel", (Guid id, ICurrentUser user, ChangeRepositoryReviewJob handler, CancellationToken ct) =>
                handler.HandleAsync(user.Id, id, false, ct).ToHttpResultAsync())
            .Audited(RepositoriesAudit.ReviewCancelled)
            .WithName("CancelRepositoryReview");
        routes.MapPost("/reviews/{id:guid}/retry", (Guid id, ICurrentUser user, ChangeRepositoryReviewJob handler, CancellationToken ct) =>
                handler.HandleAsync(user.Id, id, true, ct).ToHttpResultAsync())
            .Audited(RepositoriesAudit.ReviewRetried)
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
