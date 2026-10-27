using AiNexus.Data.App.Quality;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Http;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Quality.Evaluations;

/// <summary>刪除題組（走共用的資源生命週期）；已結束的執行保有凍結的題目副本，不受影響。</summary>
internal sealed class DeleteEvaluationSet(ResourceLifecycle lifecycle)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/sets/{id:guid}", (Guid id, ICurrentUser user, DeleteEvaluationSet handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithRequestBodyLimit(QualityModule.SetBodyLimit).Audited(QualityAudit.SetDeleted);

    public Task<Result> HandleAsync(Guid actor, Guid id, CancellationToken ct) => lifecycle.DeleteAsync(actor, id, EvaluationSet.Kind, ct);
}
