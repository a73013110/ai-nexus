using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Platform.Http;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Quality.Evaluations;

/// <summary>Who may read or edit a set, through the shared resource access list.</summary>
internal sealed class ShareEvaluationSet(ResourceAccess access)
{
    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapGet("/sets/{id:guid}/access", (Guid id, ICurrentUser user, ShareEvaluationSet handler, CancellationToken ct) => handler.GetAsync(user.Id, id, ct).ToHttpResultAsync())
            .WithRequestBodyLimit(QualityModule.SetBodyLimit);
        routes.MapPut("/sets/{id:guid}/access", (Guid id, ResourceAclRequest body, ICurrentUser user, ShareEvaluationSet handler, CancellationToken ct) => handler.SetAsync(user.Id, id, body, ct).ToHttpResultAsync())
            .WithRequestBodyLimit(QualityModule.SetBodyLimit);
    }

    public Task<Result<ResourceAclDto>> GetAsync(Guid actor, Guid id, CancellationToken ct) => access.AclAsync(actor, id, EvaluationSet.Kind, ct);

    public Task<Result> SetAsync(Guid actor, Guid id, ResourceAclRequest request, CancellationToken ct) => access.SetAclAsync(actor, id, EvaluationSet.Kind, request, ct);
}
