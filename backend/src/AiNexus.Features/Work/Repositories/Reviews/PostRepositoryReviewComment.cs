using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using FluentValidation;
using AiNexus.Features.Work.Collaboration;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Repositories.Reviews;

/// <summary>使用者預覽並編輯過的留言內容，原樣送出。</summary>
public sealed record PostRepositoryReviewCommentRequest(string Body);

internal sealed class PostRepositoryReviewCommentRequestValidator : RequestValidator<PostRepositoryReviewCommentRequest>
{
    public const int MaxLength = 20000;
    public override Error Problem => RepositoriesErrors.CommentInvalid;

    public PostRepositoryReviewCommentRequestValidator() =>
        RuleFor(x => x.Body).RequiredContent(MaxLength);
}

/// <summary>
/// 本模組唯一的 Gitea 寫入：以使用者自己的 token（需要 <c>write:issue</c> 權限）把已完成的 pull request review 貼成一則留言。
/// 送出前會先重新檢查來源；每份 review 最多貼一次，並寫入稽核紀錄。
/// </summary>
internal sealed class PostRepositoryReviewComment(AppDbContext db, RepositoryService gitea, RepositoryReviewService reviews, TimeProvider clock, AuditTrail audit) : IEndpointSlice
{
    // 必須長於一次 Gitea 請求的逾時上限（GiteaOptions.TimeoutSeconds 最多 30 秒）。
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(2);

    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/reviews/{id:guid}/comment", (Guid id, PostRepositoryReviewCommentRequest body, ICurrentUser user, PostRepositoryReviewComment handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, body.Body.Trim(), ct))
        .RequireRateLimiting(RepositoriesModule.WriteRateLimit).Audited(RepositoriesAudit.ReviewCommented).WithName("PostRepositoryReviewComment");

    public async Task<Result<RepositoryReviewDto>> HandleAsync(Guid owner, Guid id, string body, CancellationToken ct)
    {
        var row = await db.RepositoryReviews.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, ct);
        if (row is null) return RepositoriesErrors.ReviewNotFound;
        if (row.CommentUrl is not null) return RepositoriesErrors.CommentPosted;
        var finished = await db.RepositoryReviewResults.AnyAsync(x => x.ReviewId == id && x.Ordinal == RepositoryReviewPlan.ReportOrdinal, ct);
        if (row.PullRequest is not { } number || !finished) return RepositoriesErrors.CommentUnavailable;
        if (await reviews.CheckSourceAsync(owner, row, ct) is { IsSuccess: false } source) return source.Error;

        // 留言送出後無法回復，也不該開著交易等網路，所以先以條件式 UPDATE 認領，同時間只有一個請求真的送出。
        // 認領時間記在 CommentedAt（讀取端只看 CommentUrl）。程序在途中結束時要等租約到期才能重送，此時可能重複留言，但不會永遠卡住。
        var claim = clock.GetUtcNow();
        var expired = claim - ClaimLease;
        var claimed = await db.RepositoryReviews.Where(x => x.Id == id && x.CommentUrl == null && (x.CommentedAt == null || x.CommentedAt < expired))
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.CommentedAt, claim), ct);
        if (claimed == 0) return CollaborationErrors.ResourceBusy;

        Result<System.Text.Json.JsonDocument> posted;
        try { posted = await gitea.PostCommentAsync(owner, row.Repository, number, body, ct); }
        catch
        {
            await ReleaseAsync(id, claim);
            throw;
        }
        if (!posted.IsSuccess)
        {
            await ReleaseAsync(id, claim);
            return posted.Error;
        }
        using (var comment = posted.Value)
        {
            var url = GiteaJson.Text(comment.RootElement, "html_url", 500);
            row.CommentUrl = url.StartsWith(gitea.BaseUrl, StringComparison.Ordinal) ? url : gitea.Link(row.Repository) + "/pulls/" + number;
        }
        row.CommentedAt = clock.GetUtcNow();
        audit.Describe(resource: row.Id);
        await db.SaveChangesAsync(ct);
        return await reviews.DescribeAsync(row, ct);
    }

    // 送出失敗要立刻能重試；請求可能已被取消，所以這裡不傳它的 token。
    private Task ReleaseAsync(Guid id, DateTimeOffset claim) => db.RepositoryReviews.Where(x => x.Id == id && x.CommentedAt == claim)
        .ExecuteUpdateAsync(p => p.SetProperty(x => x.CommentedAt, (DateTimeOffset?)null), CancellationToken.None);
}
