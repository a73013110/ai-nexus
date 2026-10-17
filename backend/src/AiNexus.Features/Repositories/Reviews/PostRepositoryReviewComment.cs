using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Repositories.Reviews;

/// <summary>使用者預覽並編輯過的留言內容，原樣送出。</summary>
public sealed record PostRepositoryReviewCommentRequest(string Body);

internal sealed class PostRepositoryReviewCommentRequestValidator : RequestValidator<PostRepositoryReviewCommentRequest>
{
    public const int MaxLength = 20000;
    public override string ProblemCode => "review_comment_invalid";

    public PostRepositoryReviewCommentRequestValidator() =>
        RuleFor(x => x.Body).Must(x => x?.Trim().Length is > 0 and <= MaxLength).WithErrorCode("length");
}

/// <summary>
/// 本模組唯一的 Gitea 寫入：以使用者自己的 token（需要 <c>write:issue</c> 權限）把已完成的 pull request review 貼成一則留言。
/// 送出前會先重新檢查來源；每份 review 最多貼一次，並寫入稽核紀錄。
/// </summary>
internal sealed class PostRepositoryReviewComment(NexusDbContext db, RepositoryService gitea, RepositoryReviewService reviews, RepositoryWriteLock writes, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/reviews/{id:guid}/comment", (Guid id, PostRepositoryReviewCommentRequest body, ICurrentUser user, PostRepositoryReviewComment handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, body.Body.Trim(), ct).ToHttpResultAsync())
        .RequireRateLimiting(RepositoriesModule.WriteRateLimit).WithName("PostRepositoryReviewComment");

    public async Task<Result<RepositoryReviewDto>> HandleAsync(Guid owner, Guid id, string body, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        try
        {
            var row = await db.Set<RepositoryReview>().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, ct);
            if (row is null) return RepositoriesErrors.ReviewNotFound;
            if (row.CommentUrl is not null) return RepositoriesErrors.CommentPosted;
            var finished = await db.Set<RepositoryReviewResult>().AnyAsync(x => x.ReviewId == id && x.Ordinal == RepositoryReviewPlan.ReportOrdinal, ct);
            if (row.PullRequest is not { } number || !finished) return RepositoriesErrors.CommentUnavailable;
            if (await reviews.CheckSourceAsync(owner, row, ct) is { IsSuccess: false } source) return source.Error!;
            var posted = await gitea.PostCommentAsync(owner, row.Repository, number, body, ct);
            if (!posted.IsSuccess) return posted.Error!;
            using (var comment = posted.Value!)
            {
                var url = GiteaJson.Text(comment.RootElement, "html_url", 500);
                row.CommentUrl = url.StartsWith(gitea.BaseUrl, StringComparison.Ordinal) ? url : gitea.Link(row.Repository) + "/pulls/" + number;
            }
            row.CommentedAt = clock.GetUtcNow();
            db.AuditEvents.Add(new AuditEvent { OwnerId = owner, ResourceId = row.Id, Action = "repository.review.commented", Result = "posted" });
            await db.SaveChangesAsync(ct);
            return await reviews.DescribeAsync(row, ct);
        }
        finally { writes.Gate.Release(); }
    }
}
