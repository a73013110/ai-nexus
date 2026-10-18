using AiNexus.Data.App;
using AiNexus.Data.App.Repositories;
using System.Security.Cryptography;
using System.Text.Json;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Platform.Inference.ModelPolicies;
using AiNexus.Features.Platform.Inference.ModelTasks;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Features.Work.Jobs;
using AiNexus.Features.Work.Repositories.Browsing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Repositories.Reviews;

/// <summary>一個 pull request，或一個 commit（與其父 commit 比對，或以 <see cref="BaseCommit"/> 為範圍比對）。</summary>
public sealed record CreateRepositoryReviewRequest(string? Commit, string? BaseCommit, int? PullRequest, string? ModelId, string? Note, string IdempotencyKey, string Purpose = "review");

/// <summary>
/// 規則錯誤回報為 <c>review_request_invalid</c>。commit、範圍與用途有自己的公開代碼（與瀏覽端點共用），因此由 handler 檢查。
/// </summary>
internal sealed class CreateRepositoryReviewRequestValidator : RequestValidator<CreateRepositoryReviewRequest>
{
    public override string ProblemCode => "review_request_invalid";

    public CreateRepositoryReviewRequestValidator()
    {
        RuleFor(x => x.Note).Must(x => (x?.Trim().Length ?? 0) <= RepositoryReview.NoteMaxLength).WithErrorCode("length");
        RuleFor(x => x.IdempotencyKey).Must(x => Guid.TryParse(x, out _)).WithErrorCode("guid");
    }
}

/// <summary>
/// 凍結 diff、提示詞與模型設定，然後把持久化工作排入佇列。pull request 會在此時解析為 head 與 merge base，
/// 之後的 push 不會改變這次 review。request key 讓同一請求的重試回傳同一個 review；同一個 key 用於不同請求則為衝突。
/// </summary>
internal sealed class CreateRepositoryReview(AppDbContext db, RepositoryService gitea, RepositoryReviewService reviews, BrowseRepositoryIssues pulls, JobService jobs,
    RepositoryWriteLock writes, ModelCatalog catalog, ModelPolicyService policy, IOptions<InferenceOptions> inference, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder repository) => repository
        .MapPost("/reviews", (string owner, string name, CreateRepositoryReviewRequest body, ICurrentUser user, CreateRepositoryReview handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, owner + "/" + name, body, ct).ToHttpResultAsync())
        .RequireRateLimiting(RepositoriesModule.WriteRateLimit).WithName("CreateRepositoryReview");

    public async Task<Result<RepositoryReviewDto>> HandleAsync(Guid owner, string repo, CreateRepositoryReviewRequest request, CancellationToken ct)
    {
        if (!RepositoryService.IsRepository(repo)) return RepositoriesErrors.InvalidRepository;
        if (!RepositoryReviewPlan.IsPurpose(request.Purpose)) return RepositoriesErrors.ReviewPurposeInvalid;
        // 必須二選一：PR 或 commit；選 PR 時不能再指定基準 commit。
        var sourceCountInvalid = (request.PullRequest is null) == (request.Commit is null);
        var baseWithPullRequest = request.PullRequest is not null && request.BaseCommit is not null;
        if (sourceCountInvalid || baseWithPullRequest) return RepositoriesErrors.ReviewSourceInvalid;
        string head; string? basis = null;
        if (request.PullRequest is { } number)
        {
            var pull = await pulls.PullAsync(owner, repo, number, ct);
            if (!pull.IsSuccess) return pull.Error!;
            (head, basis) = (pull.Value!.HeadCommit, pull.Value.BaseCommit);
        }
        else
        {
            if (!RepositoryService.IsCommit(request.Commit)) return RepositoriesErrors.InvalidCommit;
            head = request.Commit.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(request.BaseCommit))
            {
                if (!RepositoryService.IsCommit(request.BaseCommit)) return RepositoriesErrors.InvalidCommit;
                basis = request.BaseCommit.ToLowerInvariant();
            }
        }
        if (basis == head) return RepositoriesErrors.ReviewEmptyRange;
        var note = request.Note?.Trim() ?? "";
        // 請求指紋只涵蓋會改變結果的欄位；同一個 idempotency key 若指紋不同，就視為衝突。
        var fingerprintInput = new { repo, head, basis, request.PullRequest, request.ModelId, note, request.Purpose };
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(fingerprintInput)));
        await writes.Gate.WaitAsync(ct);
        try
        {
            var existing = await db.Set<RepositoryReview>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.IdempotencyKey == request.IdempotencyKey, ct);
            if (existing is not null)
            {
                if (existing.RequestHash != hash) return RepositoriesErrors.IdempotencyConflict;
                var source = await reviews.CheckSourceAsync(owner, existing, ct);
                if (!source.IsSuccess) return source.Error;
                return await reviews.DescribeAsync(existing, ct);
            }
            var profile = await catalog.RequireAsync(request.ModelId, ct);
            if (!profile.IsSuccess) return profile.Error!;
            var model = profile.Value!;
            if (await policy.RequireAsync(owner, model.Id, ct) is { IsSuccess: false } refused) return refused.Error!;
            var headExists = await gitea.RequireCommitAsync(owner, repo, head, ct);
            if (!headExists.IsSuccess) return headExists.Error!;
            if (basis is not null && await gitea.RequireCommitAsync(owner, repo, basis, ct) is { IsSuccess: false } missing) return missing.Error!;
            var diff = await gitea.DiffAsync(owner, repo, head, basis, ct);
            if (!diff.IsSuccess) return diff.Error!;
            var snapshot = RepositoryReviewPlan.Create(diff.Value!, model, repo, head, basis, note, request.Purpose);
            if (!snapshot.IsSuccess) return snapshot.Error!;
            var row = new RepositoryReview { OwnerId = owner, Repository = repo, Commit = head, BaseCommit = basis, PullRequest = request.PullRequest,
                BaseUrl = gitea.BaseUrl, ModelId = model.Id, Note = note, ConfigurationFingerprint = ModelTaskConfiguration.Capture(model, inference.Value).Fingerprint,
                SnapshotJson = JsonSerializer.Serialize(snapshot.Value), Purpose = snapshot.Value.Purpose, IdempotencyKey = request.IdempotencyKey, RequestHash = hash, CreatedAt = clock.GetUtcNow() };
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            // 對使用者列做一次 UPDATE（值不變）以取得該列的鎖，讓同一使用者的並行建立請求依序進行；
            // 它與唯一的 request key 一起，也能防止多台應用程式主機重複建立。
            await db.Users.Where(x => x.Id == owner).ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);
            var duplicate = await db.Set<RepositoryReview>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.IdempotencyKey == request.IdempotencyKey, ct);
            if (duplicate is not null) return duplicate.RequestHash == hash ? await reviews.DescribeAsync(duplicate, ct) : RepositoriesErrors.IdempotencyConflict;
            var label = repo + (request.PullRequest is { } pr ? " · PR #" + pr : " · " + head[..10]);
            row.JobId = jobs.Enqueue(owner, null, row.Id, "repository-review", label).Id;
            db.Add(row);
            db.AuditEvents.Add(new() { OwnerId = owner, ResourceId = row.Id, Action = "repository.review.created", Result = "queued" });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return await reviews.DescribeAsync(row, ct);
        }
        finally { writes.Gate.Release(); }
    }
}
