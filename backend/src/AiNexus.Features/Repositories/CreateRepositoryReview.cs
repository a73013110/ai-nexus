using System.Security.Cryptography;
using System.Text.Json;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Repositories;

public sealed record CreateRepositoryReviewRequest(string Repository, string Commit, string? BaseCommit, string? ModelId, string? Note, string IdempotencyKey, string Purpose = "review");

/// <summary>
/// Rules reported as <c>review_request_invalid</c>. Repository, commit, range and purpose keep their own public codes,
/// shared with the query-string endpoints, so the handler checks them.
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
/// Freezes the diff, prompts and model configuration, then enqueues the durable job. The request key makes retries of
/// the same request return the same review; reusing it for a different request is a conflict.
/// </summary>
internal sealed class CreateRepositoryReview(NexusDbContext db, RepositoryService gitea, RepositoryReviewService reviews, JobService jobs, RepositoryWriteLock writes,
    ModelCatalog catalog, ModelPolicyService policy, IOptions<InferenceOptions> inference, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/reviews", async (CreateRepositoryReviewRequest body, ICurrentUser user, CreateRepositoryReview handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, body, ct)).ToHttpResult())
        .WithName("CreateRepositoryReview").Produces<RepositoryReviewDto>();

    public async Task<Result<RepositoryReviewDto>> HandleAsync(Guid owner, CreateRepositoryReviewRequest request, CancellationToken ct)
    {
        if (!RepositoryService.IsRepository(request.Repository)) return RepositoryErrors.InvalidRepository;
        if (!RepositoryService.IsCommit(request.Commit)) return RepositoryErrors.InvalidCommit;
        var basis = string.IsNullOrWhiteSpace(request.BaseCommit) ? null : request.BaseCommit.ToLowerInvariant();
        if (basis is not null && !RepositoryService.IsCommit(basis)) return RepositoryErrors.InvalidCommit;
        var head = request.Commit.ToLowerInvariant();
        if (basis == head) return RepositoryErrors.ReviewEmptyRange;
        if (!RepositoryReviewPlan.IsPurpose(request.Purpose)) return RepositoryErrors.ReviewPurposeInvalid;
        var note = request.Note?.Trim() ?? "";
        var purpose = request.Purpose;
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { request.Repository, head, basis, request.ModelId, note, purpose })));
        var legacyHash = purpose == "review" ? Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { request.Repository, head, basis, request.ModelId, note }))) : null;
        bool SameRequest(RepositoryReview existing) => existing.RequestHash == hash ||
            (existing.RequestHash == legacyHash && RepositoryReviewService.Snapshot(existing).Version == 1);
        await writes.Gate.WaitAsync(ct);
        try
        {
            var existing = await db.Set<RepositoryReview>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.IdempotencyKey == request.IdempotencyKey, ct);
            if (existing is not null)
            {
                if (!SameRequest(existing)) return RepositoryErrors.IdempotencyConflict;
                var source = await reviews.CheckSourceAsync(owner, existing, ct);
                if (!source.IsSuccess) return source.Error;
                return await reviews.DescribeAsync(existing, ct);
            }
            var model = await catalog.RequireAsync(request.ModelId, ct);
            await policy.RequireAsync(owner, model.Id, ct);
            var headExists = await gitea.RequireCommitAsync(owner, request.Repository, head, ct);
            if (!headExists.IsSuccess) return headExists.Error;
            if (basis is not null)
            {
                var basisExists = await gitea.RequireCommitAsync(owner, request.Repository, basis, ct);
                if (!basisExists.IsSuccess) return basisExists.Error;
            }
            var diff = await gitea.DiffAsync(owner, request.Repository, head, basis, ct);
            if (!diff.IsSuccess) return diff.Error;
            var snapshot = RepositoryReviewPlan.Create(diff.Value, model, request.Repository, head, basis, note, purpose);
            var row = new RepositoryReview { OwnerId = owner, Repository = request.Repository, Commit = head, BaseCommit = basis,
                BaseUrl = gitea.BaseUrl, ModelId = model.Id, Note = note, ConfigurationFingerprint = ModelTaskConfiguration.Capture(model, inference.Value).Fingerprint,
                SnapshotJson = JsonSerializer.Serialize(snapshot), IdempotencyKey = request.IdempotencyKey, RequestHash = hash, CreatedAt = clock.GetUtcNow() };
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            // This account lock and unique request key also protect multiple application hosts.
            await db.Users.Where(x => x.Id == owner).ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);
            var duplicate = await db.Set<RepositoryReview>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.IdempotencyKey == request.IdempotencyKey, ct);
            if (duplicate is not null)
            {
                if (!SameRequest(duplicate)) return RepositoryErrors.IdempotencyConflict;
                return await reviews.DescribeAsync(duplicate, ct);
            }
            row.JobId = jobs.Enqueue(owner, null, row.Id, "repository-review", request.Repository + " · " + head[..10]).Id;
            db.Add(row);
            db.AuditEvents.Add(new() { OwnerId = owner, ResourceId = row.Id, Action = "repository.review.created", Result = "queued" });
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return await reviews.DescribeAsync(row, ct);
        }
        finally { writes.Gate.Release(); }
    }
}
