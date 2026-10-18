using AiNexus.Data.App;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Quality;
using System.Text.Json;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Quality.Evaluations;

/// <summary>編輯時需帶上所依據的 <c>ExpectedVersion</c>；資料庫已有更新的版本時即為衝突。</summary>
public sealed record EvaluationSetRequest(string Name, string Description, IReadOnlyList<EvaluationCase> Cases, int ExpectedVersion = 1);

/// <summary>
/// 描述與案例的規則。名稱由 handler 先以專屬代碼回報，因此這些規則只在名稱合法後才套用。
/// </summary>
internal sealed class EvaluationSetRequestValidator : RequestValidator<EvaluationSetRequest>
{
    public override string ProblemCode => "evaluation_cases_invalid";

    public EvaluationSetRequestValidator()
    {
        When(x => ResourceAccess.IsValidName(x.Name), () =>
        {
            RuleFor(x => x.Description).MaximumLength(2000);
            RuleFor(x => x.Cases).Must(x => x is { Count: >= 1 and <= 20 }).WithErrorCode("count");
            RuleForEach(x => x.Cases).NotNull().ChildRules(test =>
            {
                test.RuleFor(x => x.Question).Must(x => x.Trim().Length is >= 1 and <= 4000).WithErrorCode("length");
                test.RuleFor(x => x.Reference).MaximumLength(4000);
                test.RuleFor(x => x.RequiredTerms).Must(TermsValid).WithErrorCode("terms");
                test.RuleFor(x => x.ForbiddenTerms).Must(TermsValid).WithErrorCode("terms");
            });
        });
    }

    // 詞彙最多 20 個，去除空白後不分大小寫不得重複，每個 1 至 80 字元。
    private static bool TermsValid(IReadOnlyList<string>? terms) => terms is null || terms.Count <= 20 && terms.All(x => x.Trim().Length is > 0 and <= 80) && terms.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == terms.Count;
}

/// <summary>建立個人題組（每人上限 100 個），或為可編輯的既有題組儲存新版本。</summary>
internal sealed class SaveEvaluationSet(AppDbContext db, ResourceAccess access, ResourceWriteLock writes, TimeProvider clock)
{
    public static RouteHandlerBuilder MapCreate(RouteGroupBuilder routes) => routes
        .MapPost("/sets", async (EvaluationSetRequest body, ICurrentUser user, SaveEvaluationSet handler, CancellationToken ct) => (await handler.HandleAsync(user.Id, null, body, ct)).ToHttpResult())
        .WithRequestBodyLimit(QualityModule.SetBodyLimit);

    public static RouteHandlerBuilder MapUpdate(RouteGroupBuilder routes) => routes
        .MapPut("/sets/{id:guid}", async (Guid id, EvaluationSetRequest body, ICurrentUser user, SaveEvaluationSet handler, CancellationToken ct) => (await handler.HandleAsync(user.Id, id, body, ct)).ToHttpResult())
        .WithRequestBodyLimit(QualityModule.SetBodyLimit);

    public async Task<Result<EvaluationSetDto>> HandleAsync(Guid actor, Guid? id, EvaluationSetRequest request, CancellationToken ct)
    {
        if (ResourceAccess.Name(request.Name) is not { IsSuccess: true } name) return QualityErrors.InvalidName;
        // 更新只鎖該題組；建立則改鎖擁有者，讓上限計數在並行建立時仍然準確。
        using (await (id is Guid key ? writes.AcquireAsync(key, ct) : writes.AcquireAsync("evaluation-sets", actor, ct)))
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct); Resource resource;
            if (id is Guid existing)
            {
                var allowed = await access.RequireAsync(actor, existing, EvaluationSet.Kind, ct, write: true);
                if (!allowed.IsSuccess) return allowed.Error;
                resource = allowed.Value;
                var changed = await db.Set<EvaluationSet>().Where(x => x.Id == existing && x.Version == request.ExpectedVersion).ExecuteUpdateAsync(p => p.SetProperty(x => x.Version, x => x.Version + 1).SetProperty(x => x.Description, request.Description.Trim()).SetProperty(x => x.CasesJson, JsonSerializer.Serialize(request.Cases, (JsonSerializerOptions?)null)), ct);
                if (changed != 1) return QualityErrors.SetConflict;
                resource.Name = name.Value; resource.UpdatedAt = clock.GetUtcNow();
            }
            else
            {
                if (await db.Set<Resource>().CountAsync(x => x.Kind == EvaluationSet.Kind && x.OwnerId == actor, ct) >= 100) return QualityErrors.SetLimit;
                resource = new() { OwnerId = actor, Kind = EvaluationSet.Kind, Name = name.Value }; db.Add(resource);
                db.Add(new EvaluationSet { Id = resource.Id, Description = request.Description.Trim(), CasesJson = JsonSerializer.Serialize(request.Cases) });
            }
            db.AuditEvents.Add(new() { OwnerId = actor, Action = "quality.set.saved", ResourceId = resource.Id, Result = "saved" });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return await access.LoadSetAsync(db, actor, resource.Id, ct);
        }
    }
}
