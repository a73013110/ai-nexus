using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Platform.Http;

namespace AiNexus.Features.Quality.Evaluations;

/// <summary>Who may read or edit a set, through the shared resource access list.</summary>
internal static class ShareEvaluationSet
{
    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapGet("/sets/{id:guid}/access", async (Guid id, ICurrentUser user, ResourceAccess access, CancellationToken ct) => Results.Ok(await access.AclAsync(user.Id, id, EvaluationSet.Kind, ct)))
            .Produces<ResourceAclDto>().WithRequestBodyLimit(QualityModule.SetBodyLimit);
        routes.MapPut("/sets/{id:guid}/access", async (Guid id, ResourceAclRequest body, ICurrentUser user, ResourceAccess access, CancellationToken ct) =>
        {
            await access.SetAclAsync(user.Id, id, EvaluationSet.Kind, body, ct);
            return Results.NoContent();
        }).WithRequestBodyLimit(QualityModule.SetBodyLimit);
    }
}
