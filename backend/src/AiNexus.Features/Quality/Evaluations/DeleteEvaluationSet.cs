using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Platform.Http;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Quality.Evaluations;

/// <summary>Deletes a set through the shared resource lifecycle; finished runs keep their frozen copy of the cases.</summary>
internal sealed class DeleteEvaluationSet(ResourceLifecycle lifecycle)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/sets/{id:guid}", (Guid id, ICurrentUser user, DeleteEvaluationSet handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithRequestBodyLimit(QualityModule.SetBodyLimit);

    public Task<Result> HandleAsync(Guid actor, Guid id, CancellationToken ct) => lifecycle.DeleteAsync(actor, id, EvaluationSet.Kind, ct);
}
