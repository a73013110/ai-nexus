using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Repositories;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Repositories.Reviews;

/// <summary>使用者在目前 Gitea 主機上，某個程式庫最近 50 筆 review。</summary>
internal sealed class ListRepositoryReviews(AppDbContext db, RepositoryService gitea, RepositoryReviewService reviews)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder repository) => repository
        .MapGet("/reviews", (string owner, string name, ICurrentUser user, ListRepositoryReviews handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, owner + "/" + name, ct).ToHttpResultAsync())
        .WithName("ListRepositoryReviews");

    public async Task<Result<IReadOnlyList<RepositoryReviewDto>>> HandleAsync(Guid owner, string repo, CancellationToken ct)
    {
        if (!RepositoryService.IsRepository(repo)) return RepositoriesErrors.InvalidRepository;
        // 只查列表需要的欄位；凍結的快照（每筆最多一百萬字元）留在資料庫，不載入記憶體。
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
