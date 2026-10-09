using System.Text.Json;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Knowledge;
using AiNexus.Features.Operations;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Quality;

public sealed record RetrievalEvaluationRequest(string Title, IReadOnlyList<Guid> CollectionIds, IReadOnlyList<RetrievalEvaluationCase> Cases);

/// <summary>
/// The request's outline. The per-case annotations are checked by the handler because they report the corpus code
/// (<c>retrieval_corpus_invalid</c>) that the document and page checks share.
/// </summary>
internal sealed class RetrievalEvaluationRequestValidator : RequestValidator<RetrievalEvaluationRequest>
{
    public override string ProblemCode => "retrieval_evaluation_invalid";

    public RetrievalEvaluationRequestValidator()
    {
        RuleFor(x => x.Title).Must(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 120).WithErrorCode("length");
        RuleFor(x => x.CollectionIds).Must(x => x is { Count: >= 1 and <= 3 }).WithErrorCode("count");
        RuleFor(x => x.Cases).Must(x => x is { Count: >= 1 and <= 20 }).WithErrorCode("count");
    }
}

/// <summary>Freezes a retrieval acceptance set with a fingerprint of the index and settings, and queues it.</summary>
internal sealed class CreateRetrievalEvaluation(NexusDbContext db, RetrievalEvaluationService evaluations, EmbeddingProfiles profiles, IOptions<KnowledgeOptions> options,
    ResourceWriteLock writes, JobService jobs, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/retrieval-evals", async (RetrievalEvaluationRequest body, ICurrentUser user, CreateRetrievalEvaluation handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, body, ct)).ToHttpResult())
        .Produces<RetrievalEvaluationDto>();

    public async Task<Result<RetrievalEvaluationDto>> HandleAsync(Guid actor, RetrievalEvaluationRequest request, CancellationToken ct)
    {
        if (!AnnotationsValid(request.Cases)) return QualityErrors.CorpusInvalid;
        await evaluations.RequireAccessAsync(actor, request.CollectionIds, ct);
        var ids = request.Cases.SelectMany(x => x.Relevant).Select(x => x.DocumentId).Distinct().ToArray();
        var documents = await db.Set<KnowledgeDocument>().AsNoTracking().Where(x => ids.Contains(x.Id) && !x.IsDeleted && x.Status == "ready" && x.CollectionId != null && request.CollectionIds.Contains(x.CollectionId.Value)).Select(x => x.Id).ToListAsync(ct);
        if (documents.Count != ids.Length) return QualityErrors.CorpusInvalid;
        var pages = await db.Set<DocumentPage>().AsNoTracking().Where(x => ids.Contains(x.DocumentId)).Select(x => new { x.DocumentId, x.PageNumber }).ToListAsync(ct);
        if (request.Cases.SelectMany(x => x.Relevant).Any(x => x.Pages.Any(p => !pages.Any(page => page.DocumentId == x.DocumentId && page.PageNumber == p)))) return QualityErrors.PagesInvalid;
        await profiles.ActiveAsync(ct);
        // The owner's one active retrieval evaluation and run limit.
        using (await writes.AcquireAsync("retrieval-evaluations", actor, ct))
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await evaluations.RequireAccessAsync(actor, request.CollectionIds, ct);
            if (await db.Set<BackgroundJob>().AnyAsync(x => x.OwnerId == actor && x.Kind == "retrieval-eval" && x.ActiveKey != null, ct)) return QualityErrors.RetrievalActive;
            if (await db.Set<RetrievalEvaluation>().CountAsync(x => x.OwnerId == actor, ct) >= 500) return QualityErrors.RetrievalLimit;
            var profile = await profiles.ActiveAsync(ct);
            var json = RetrievalEvaluationService.Json;
            var run = new RetrievalEvaluation { OwnerId = actor, Title = request.Title.Trim(), CollectionsJson = JsonSerializer.Serialize(request.CollectionIds, json), CasesJson = JsonSerializer.Serialize(request.Cases, json),
                TopK = options.Value.TopK, ProfileKey = profile.Key, ConfigurationFingerprint = await evaluations.FingerprintAsync(profile, request.CollectionIds, ct), CreatedAt = clock.GetUtcNow() };
            var job = jobs.Enqueue(actor, null, run.Id, "retrieval-eval", "檢索評測 · " + run.Title); run.JobId = job.Id; db.Add(run);
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = run.Id, Action = "quality.retrieval.queued", Result = "queued" });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return RetrievalEvaluationService.Describe(run, job);
        }
    }

    // Unique case ids and bounded queries; answerable cases name graded documents with positive, distinct pages; no-answer cases name none.
    private static bool AnnotationsValid(IReadOnlyList<RetrievalEvaluationCase> cases)
        => !cases.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id) || x.Id.Length > 80 || string.IsNullOrWhiteSpace(x.Query) || x.Query.Length > 2000 || x.Relevant is null
               || x.Relevant.Count > 100 || (x.NoAnswer ? x.Relevant.Count != 0 : x.Relevant.Count == 0)
               || x.Relevant.Any(r => r is null || r.DocumentId == Guid.Empty || r.Grade is < 1 or > 3 || r.Pages is null || r.Pages.Count > 100 || r.Pages.Any(p => p < 1) || r.Pages.Distinct().Count() != r.Pages.Count)
               || x.Relevant.Select(r => r.DocumentId).Distinct().Count() != x.Relevant.Count)
           && cases.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() == cases.Count;
}
