using AiNexus.Data.App;
using AiNexus.Data.App.Quality;
using System.Text.Json;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Platform.Inference.ModelPolicies;
using AiNexus.Features.Platform.Inference.ModelTasks;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Features.Work.Jobs;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Validation;
using FluentValidation;
using AiNexus.Features.Admin.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Quality.Evaluations;

public sealed record EvaluationVariantRequest(string Label, string? ModelId = null, string Instruction = "");

public sealed record EvaluationRunRequest(IReadOnlyList<EvaluationVariantRequest> Variants);

internal sealed class EvaluationRunRequestValidator : RequestValidator<EvaluationRunRequest>
{
    private static readonly RuleCode Unique = new("unique");

    public override Error Problem => QualityErrors.VariantsInvalid;

    // 變體 1 至 3 個；標籤去除空白後不分大小寫不得重複，每個 1 至 80 字元。
    public EvaluationRunRequestValidator()
    {
        RuleFor(x => x.Variants).Must(x => x is { Count: >= 1 and <= 3 }).WithErrorCode(RuleCode.Count);
        RuleForEach(x => x.Variants).NotNull().ChildRules(variant =>
        {
            variant.RuleFor(x => x.Label).RequiredText(80);
            variant.RuleFor(x => x.Instruction).OptionalContent(4000);
        });
        RuleFor(x => x.Variants).Must(x => x.Select(v => v?.Label.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == x.Count).WithErrorCode(Unique)
            .When(x => x.Variants is not null);
    }
}

/// <summary>凍結題組的案例與各變體的模型設定，排入比較工作；每位使用者同時只能有一個進行中的評測。</summary>
internal sealed class StartEvaluationRun(AppDbContext db, ResourceAccess access, ContainerLock containers, JobService jobs, ModelCatalog models, ModelPolicyService policy,
    IOptions<InferenceOptions> inference, TimeProvider clock, AuditTrail audit)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/sets/{id:guid}/runs", (Guid id, EvaluationRunRequest body, ICurrentUser user, StartEvaluationRun handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, body, ct))
        .WithRequestBodyLimit(QualityModule.SetBodyLimit).Audited(QualityAudit.RunQueued);

    public async Task<Result<EvaluationRunDto>> HandleAsync(Guid actor, Guid setId, EvaluationRunRequest request, CancellationToken ct)
    {
        var variants = new List<EvaluationVariant>();
        foreach (var variant in request.Variants)
        {
            var model = await models.RequireAsync(variant.ModelId, ct);
            if (!model.IsSuccess) return model.Error;
            if (await policy.RequireAsync(actor, model.Value.Id, ct) is { IsSuccess: false } refused) return refused.Error;
            variants.Add(new(variant.Label.Trim(), model.Value.Id, variant.Instruction.Trim(), ModelTaskConfiguration.Capture(model.Value, inference.Value)));
        }
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await containers.TryStartWorkAsync(setId, ct)) return CollaborationErrors.ResourceBusy;
        // 上限與「一人一個進行中」都是每人的規則，所以鎖擁有者。題組內容是單列快照讀取，不必與儲存題組排隊。
        await db.LockUserRowAsync(actor, ct);
        var allowed = await access.RequireAsync(actor, setId, EvaluationSet.Kind, ct);
        if (!allowed.IsSuccess) return allowed.Error;
        var resource = allowed.Value;
        if (await db.BackgroundJobs.AnyAsync(x => x.OwnerId == actor && x.Kind == EvaluationHandler.JobKind && x.ActiveKey != null, ct)) return QualityErrors.EvaluationActive;
        if (await db.EvaluationRuns.CountAsync(x => x.OwnerId == actor, ct) >= 500) return QualityErrors.RunLimit;
        var set = await db.EvaluationSets.AsNoTracking().SingleAsync(x => x.Id == setId, ct);
        var run = new EvaluationRun { SetId = setId, OwnerId = actor, SetTitle = resource.Name, SetVersion = set.Version, CasesJson = set.CasesJson, VariantsJson = JsonSerializer.Serialize(variants), CreatedAt = clock.GetUtcNow() };
        var job = jobs.Enqueue(actor, setId, run.Id, EvaluationHandler.JobKind, "品質評測 · " + resource.Name); run.JobId = job.Id; db.Add(run);
        audit.Describe(resource: run.Id);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return run.ToDto(job, actor);
    }
}
