using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Quality;
using System.Text.Json;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Platform.Inference.ModelPolicies;
using AiNexus.Features.Platform.Inference.ModelTasks;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Features.Work.Jobs;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Quality.Evaluations;

public sealed record EvaluationVariantRequest(string Label, string? ModelId = null, string Instruction = "");

public sealed record EvaluationRunRequest(IReadOnlyList<EvaluationVariantRequest> Variants);

internal sealed class EvaluationRunRequestValidator : RequestValidator<EvaluationRunRequest>
{
    public override string ProblemCode => "evaluation_variants_invalid";

    // 變體 1 至 3 個；標籤去除空白後不分大小寫不得重複，每個 1 至 80 字元。
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

/// <summary>凍結題組的案例與各變體的模型設定，排入比較工作；每位使用者同時只能有一個進行中的評測。</summary>
internal sealed class StartEvaluationRun(AppDbContext db, ResourceAccess access, ResourceWriteLock writes, JobService jobs, ModelCatalog models, ModelPolicyService policy,
    IOptions<InferenceOptions> inference, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/sets/{id:guid}/runs", async (Guid id, EvaluationRunRequest body, ICurrentUser user, StartEvaluationRun handler, CancellationToken ct) => (await handler.HandleAsync(user.Id, id, body, ct)).ToHttpResult())
        .WithRequestBodyLimit(QualityModule.SetBodyLimit);

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
        // 固定先取擁有者的鎖、後取題組的鎖；反向取鎖會與其他請求死鎖。上限與進行中檢查也都在鎖內完成。
        using (await writes.AcquireAsync("evaluation-runs", actor, ct))
        using (await writes.AcquireAsync(setId, ct))
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var allowed = await access.RequireAsync(actor, setId, EvaluationSet.Kind, ct);
            if (!allowed.IsSuccess) return allowed.Error;
            var resource = allowed.Value;
            if (await db.Set<BackgroundJob>().AnyAsync(x => x.OwnerId == actor && x.Kind == EvaluationHandler.JobKind && x.ActiveKey != null, ct)) return QualityErrors.EvaluationActive;
            if (await db.Set<EvaluationRun>().CountAsync(x => x.OwnerId == actor, ct) >= 500) return QualityErrors.RunLimit;
            var set = await db.Set<EvaluationSet>().AsNoTracking().SingleAsync(x => x.Id == setId, ct);
            var run = new EvaluationRun { SetId = setId, OwnerId = actor, SetTitle = resource.Name, SetVersion = set.Version, CasesJson = set.CasesJson, VariantsJson = JsonSerializer.Serialize(variants), CreatedAt = clock.GetUtcNow() };
            var job = jobs.Enqueue(actor, setId, run.Id, EvaluationHandler.JobKind, "品質評測 · " + resource.Name); run.JobId = job.Id; db.Add(run);
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = run.Id, Action = "quality.run.queued", Result = "queued" });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return run.ToDto(job, actor);
        }
    }
}
