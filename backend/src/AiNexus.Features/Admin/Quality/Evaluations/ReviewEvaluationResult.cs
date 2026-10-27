using AiNexus.Data.App;
using AiNexus.Data.App.Quality;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Quality.Evaluations;

/// <summary>Score 為 null 即清除人工分數；備註則每次都以本次內容覆寫。</summary>
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

/// <summary>對執行中單一案例與變體給予人工分數；需要題組的編輯權限。</summary>
internal sealed class ReviewEvaluationResult(AppDbContext db, ResourceAccess access, AuditTrail audit)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPut("/runs/{id:guid}/results/{caseIndex:int}/{variantIndex:int}/review", async (Guid id, int caseIndex, int variantIndex, ReviewRequest body, ICurrentUser user, ReviewEvaluationResult handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, caseIndex, variantIndex, body, ct)).ToHttpResult()).Audited(QualityAudit.ResultReviewed);

    public async Task<Result> HandleAsync(Guid actor, Guid id, int caseIndex, int variantIndex, ReviewRequest request, CancellationToken ct)
    {
        var run = await db.Set<EvaluationRun>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (run is null) return QualityErrors.ItemMissing;
        if (await access.RequireAsync(actor, run.SetId, EvaluationSet.Kind, ct, write: true) is { IsSuccess: false } denied) return denied.Error;
        var result = await db.Set<EvaluationResult>().SingleOrDefaultAsync(x => x.RunId == id && x.CaseIndex == caseIndex && x.VariantIndex == variantIndex, ct);
        if (result is null) return QualityErrors.ItemMissing;
        result.ReviewScore = request.Score; result.ReviewNote = request.Note.Trim(); result.ReviewerId = actor;
        audit.Describe(details: new { caseIndex, variantIndex, score = request.Score });
        await db.SaveChangesAsync(ct);
        return Result.Success;
    }
}
