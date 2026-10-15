using System.Security.Cryptography;
using System.Text.Json;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Inference.ModelPolicies;
using AiNexus.Features.Inference.ModelTasks;
using AiNexus.Features.Inference.Models;
using AiNexus.Features.Jobs;
using AiNexus.Features.Persistence;
using AiNexus.Features.Repositories.Browsing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Repositories.Reviews;

/// <summary>A pull request, or a commit (against its parent, or against <see cref="BaseCommit"/> as a range).</summary>
public sealed record CreateRepositoryReviewRequest(string? Commit, string? BaseCommit, int? PullRequest, string? ModelId, string? Note, string IdempotencyKey, string Purpose = "review");

/// <summary>
/// Rules reported as <c>review_request_invalid</c>. Commits, range and purpose keep their own public codes, shared with the
/// browsing endpoints, so the handler checks them.
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
/// Freezes the diff, prompts and model configuration, then enqueues the durable job. A pull request is resolved now to
/// its head and merge base, so later pushes do not change the review. The request key makes retries of the same request
/// return the same review; reusing it for a different request is a conflict.
/// </summary>
internal sealed class CreateRepositoryReview(NexusDbContext db, RepositoryService gitea, RepositoryReviewService reviews, BrowseRepositoryIssues pulls, JobService jobs,
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
        if ((request.PullRequest is null) == (request.Commit is null) || (request.PullRequest is not null && request.BaseCommit is not null))
            return RepositoriesErrors.ReviewSourceInvalid;
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
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { repo, head, basis, request.PullRequest, request.ModelId, note, request.Purpose })));
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
            // This account lock and unique request key also protect multiple application hosts.
            await db.Users.Where(x => x.Id == owner).ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);
            var duplicate = await db.Set<RepositoryReview>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.IdempotencyKey == request.IdempotencyKey, ct);
            if (duplicate is not null) return duplicate.RequestHash == hash ? await reviews.DescribeAsync(duplicate, ct) : RepositoriesErrors.IdempotencyConflict;
            var label = repo + (request.PullRequest is { } pr ? " · PR #" + pr : " · " + head[..10]);
            row.JobId = jobs.Enqueue(owner, null, row.Id, "repository-review", label).Id;
            db.Add(row);
            db.AuditEvents.Add(new() { OwnerId = owner, ResourceId = row.Id, Action = "repository.review.created", Result = "queued" });
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return await reviews.DescribeAsync(row, ct);
        }
        finally { writes.Gate.Release(); }
    }
}
