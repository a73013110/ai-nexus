using System.Text.Json;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Quality.Evaluations;

/// <summary>An edit names the <c>ExpectedVersion</c> it was based on; a newer saved version is a conflict.</summary>
public sealed record EvaluationSetRequest(string Name, string Description, IReadOnlyList<EvaluationCase> Cases, int ExpectedVersion = 1);

/// <summary>
/// Description and cases. An invalid name is reported first, with its own code, by the handler, so these rules only
/// apply once the name is valid.
/// </summary>
internal sealed class EvaluationSetRequestValidator : RequestValidator<EvaluationSetRequest>
{
    public override string ProblemCode => "evaluation_cases_invalid";

    public EvaluationSetRequestValidator()
    {
        When(x => SaveEvaluationSet.NameIsValid(x.Name), () =>
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

    // At most 20 distinct (case-insensitive) terms of 1 to 80 characters.
    private static bool TermsValid(IReadOnlyList<string>? terms) => terms is null || terms.Count <= 20 && terms.All(x => x.Trim().Length is > 0 and <= 80) && terms.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == terms.Count;
}

/// <summary>Creates a personal set (at most 100), or saves a new version of one the user may edit.</summary>
internal sealed class SaveEvaluationSet(NexusDbContext db, ResourceAccess access, ResourceWriteLock writes, TimeProvider clock)
{
    public static RouteHandlerBuilder MapCreate(RouteGroupBuilder routes) => routes
        .MapPost("/sets", async (EvaluationSetRequest body, ICurrentUser user, SaveEvaluationSet handler, CancellationToken ct) => (await handler.HandleAsync(user.Id, null, body, ct)).ToHttpResult())
        .Produces<EvaluationSetDto>().WithRequestBodyLimit(QualityModule.SetBodyLimit);

    public static RouteHandlerBuilder MapUpdate(RouteGroupBuilder routes) => routes
        .MapPut("/sets/{id:guid}", async (Guid id, EvaluationSetRequest body, ICurrentUser user, SaveEvaluationSet handler, CancellationToken ct) => (await handler.HandleAsync(user.Id, id, body, ct)).ToHttpResult())
        .Produces<EvaluationSetDto>().WithRequestBodyLimit(QualityModule.SetBodyLimit);

    /// <summary>The rule of <see cref="ResourceAccess.Name"/>.</summary>
    internal static bool NameIsValid(string? name) => name?.Trim() is { Length: >= 1 and <= 120 } trimmed && !trimmed.Any(char.IsControl);

    public async Task<Result<EvaluationSetDto>> HandleAsync(Guid actor, Guid? id, EvaluationSetRequest request, CancellationToken ct)
    {
        if (!NameIsValid(request.Name)) return QualityErrors.InvalidName;
        // An update is per set; a create checks the owner's set limit.
        using (await (id is Guid key ? writes.AcquireAsync(key, ct) : writes.AcquireAsync("evaluation-sets", actor, ct)))
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct); WorkspaceResource resource;
            if (id is Guid existing)
            {
                resource = (await access.RequireAsync(actor, existing, EvaluationSet.Kind, ct, write: true)).OrThrow();
                var changed = await db.Set<EvaluationSet>().Where(x => x.Id == existing && x.Version == request.ExpectedVersion).ExecuteUpdateAsync(p => p.SetProperty(x => x.Version, x => x.Version + 1).SetProperty(x => x.Description, request.Description.Trim()).SetProperty(x => x.CasesJson, JsonSerializer.Serialize(request.Cases, (JsonSerializerOptions?)null)), ct);
                if (changed != 1) return QualityErrors.SetConflict;
                resource.Name = ResourceAccess.Name(request.Name).OrThrow(); resource.UpdatedAt = clock.GetUtcNow();
            }
            else
            {
                if (await db.Set<WorkspaceResource>().CountAsync(x => x.Kind == EvaluationSet.Kind && x.OwnerId == actor, ct) >= 100) return QualityErrors.SetLimit;
                resource = new() { OwnerId = actor, Kind = EvaluationSet.Kind, Name = ResourceAccess.Name(request.Name).OrThrow() }; db.Add(resource);
                db.Add(new EvaluationSet { Id = resource.Id, Description = request.Description.Trim(), CasesJson = JsonSerializer.Serialize(request.Cases) });
            }
            db.AuditEvents.Add(new() { OwnerId = actor, Action = "quality.set.saved", ResourceId = resource.Id, Result = "saved" });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return await access.LoadSetAsync(db, actor, resource.Id, ct);
        }
    }
}
