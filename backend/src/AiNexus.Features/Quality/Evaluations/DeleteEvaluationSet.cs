using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Platform.Http;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Quality.Evaluations;

/// <summary>Deletes a set through the shared resource lifecycle; finished runs keep their frozen copy of the cases.</summary>
internal static class DeleteEvaluationSet
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/sets/{id:guid}", async (Guid id, ICurrentUser user, ResourceLifecycle lifecycle, CancellationToken ct) =>
        {
            (await lifecycle.DeleteAsync(user.Id, id, EvaluationSet.Kind, ct)).OrThrow();
            return Results.NoContent();
        })
        .WithRequestBodyLimit(QualityModule.SetBodyLimit);
}
