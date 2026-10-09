using System.Text.Json;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Quality.Evaluations;

public sealed record EvaluationVariantRequest(string Label, string? ModelId = null, string Instruction = "");

public sealed record EvaluationRunRequest(IReadOnlyList<EvaluationVariantRequest> Variants);

internal sealed class EvaluationRunRequestValidator : RequestValidator<EvaluationRunRequest>
{
    public override string ProblemCode => "evaluation_variants_invalid";

    // One to three variants with distinct (case-insensitive) labels of 1 to 80 characters.
    public EvaluationRunRequestValidator()
    {
        RuleFor(x => x.Variants).Must(x => x is { Count: >= 1 and <= 3 }).WithErrorCode("count");
        RuleForEach(x => x.Variants).NotNull().ChildRules(variant =>
        {
            variant.RuleFor(x => x.Label).Must(x => x.Trim().Length is >= 1 and <= 80).WithErrorCode("length");
            variant.RuleFor(x => x.Instruction).MaximumLength(4000);
        });
        RuleFor(x => x.Variants).Must(x => x.Select(v => v?.Label.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == x.Count).WithErrorCode("unique")
            .When(x => x.Variants is not null);
    }
}

/// <summary>Freezes the set's cases and each variant's model settings, and queues the comparison (one active per user).</summary>
internal sealed class StartEvaluationRun(NexusDbContext db, ResourceAccess access, ResourceWriteLock writes, JobService jobs, ModelCatalog models, ModelPolicyService policy,
    IOptions<InferenceOptions> inference, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/sets/{id:guid}/runs", async (Guid id, EvaluationRunRequest body, ICurrentUser user, StartEvaluationRun handler, CancellationToken ct) => (await handler.HandleAsync(user.Id, id, body, ct)).ToHttpResult())
        .Produces<EvaluationRunDto>().WithRequestBodyLimit(QualityModule.SetBodyLimit);

    public async Task<Result<EvaluationRunDto>> HandleAsync(Guid actor, Guid setId, EvaluationRunRequest request, CancellationToken ct)
    {
        var variants = new List<EvaluationVariant>();
        foreach (var variant in request.Variants)
        {
            var model = (await models.RequireAsync(variant.ModelId, ct)).OrThrow(); (await policy.RequireAsync(actor, model.Id, ct)).OrThrow();
            variants.Add(new(variant.Label.Trim(), model.Id, variant.Instruction.Trim(), ModelTaskConfiguration.Capture(model, inference.Value)));
        }
        // The owner's one active evaluation and run limit, then the set (always in this order).
        using (await writes.AcquireAsync("evaluation-runs", actor, ct))
        using (await writes.AcquireAsync(setId, ct))
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var resource = (await access.RequireAsync(actor, setId, EvaluationSet.Kind, ct)).OrThrow();
            if (await db.Set<BackgroundJob>().AnyAsync(x => x.OwnerId == actor && x.Kind == "evaluation" && x.ActiveKey != null, ct)) return QualityErrors.EvaluationActive;
            if (await db.Set<EvaluationRun>().CountAsync(x => x.OwnerId == actor, ct) >= 500) return QualityErrors.RunLimit;
            var set = await db.Set<EvaluationSet>().AsNoTracking().SingleAsync(x => x.Id == setId, ct);
            var run = new EvaluationRun { SetId = setId, OwnerId = actor, SetTitle = resource.Name, SetVersion = set.Version, CasesJson = set.CasesJson, VariantsJson = JsonSerializer.Serialize(variants), CreatedAt = clock.GetUtcNow() };
            var job = jobs.Enqueue(actor, setId, run.Id, "evaluation", "品質評測 · " + resource.Name); run.JobId = job.Id; db.Add(run);
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = run.Id, Action = "quality.run.queued", Result = "queued" });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return run.ToDto(job, actor);
        }
    }
}
