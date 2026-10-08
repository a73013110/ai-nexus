using System.Text.Json;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Quality;

/// <summary>A null <c>Score</c> clears the human score.</summary>
public sealed record ReviewRequest(int? Score, string Note = "");

internal sealed class ReviewRequestValidator : RequestValidator<ReviewRequest>
{
    public override string ProblemCode => "review_invalid";

    public ReviewRequestValidator()
    {
        RuleFor(x => x.Score).Must(x => x is null or (>= 1 and <= 5)).WithErrorCode("range");
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}

/// <summary>A human score for one case and variant of a run; requires edit access to the set.</summary>
internal sealed class ReviewEvaluationResult(NexusDbContext db, ResourceAccess access)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPut("/runs/{id:guid}/results/{caseIndex:int}/{variantIndex:int}/review", async (Guid id, int caseIndex, int variantIndex, ReviewRequest body, ICurrentUser user, ReviewEvaluationResult handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, caseIndex, variantIndex, body, ct)).ToHttpResult());

    public async Task<Result> HandleAsync(Guid actor, Guid id, int caseIndex, int variantIndex, ReviewRequest request, CancellationToken ct)
    {
        var run = await db.Set<EvaluationRun>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (run is null) return QualityErrors.ItemMissing;
        await access.RequireAsync(actor, run.SetId, EvaluationSet.Kind, ct, write: true);
        var result = await db.Set<EvaluationResult>().SingleOrDefaultAsync(x => x.RunId == id && x.CaseIndex == caseIndex && x.VariantIndex == variantIndex, ct);
        if (result is null) return QualityErrors.ItemMissing;
        result.ReviewScore = request.Score; result.ReviewNote = request.Note.Trim(); result.ReviewerId = actor;
        db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "quality.result.reviewed", Result = "saved", DetailsJson = JsonSerializer.Serialize(new { caseIndex, variantIndex, request.Score }) });
        await db.SaveChangesAsync(ct);
        return Result.Success;
    }
}
