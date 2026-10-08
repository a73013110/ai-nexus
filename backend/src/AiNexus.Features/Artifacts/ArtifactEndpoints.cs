using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;

namespace AiNexus.Features.Artifacts;

public static class ArtifactEndpoints
{
    public static void MapArtifacts(this RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/artifacts").RequireAuthorization("feature:artifacts").WithTags("Artifacts");
        routes.MapGet("", async (CurrentUser current, ArtifactService service, CancellationToken ct) => await service.ListAsync((await current.GetAsync(ct)).Id, ct)).WithName("ListArtifacts").Produces<IReadOnlyList<ArtifactSummaryDto>>();
        routes.MapPost("", async (CreateArtifactRequest request, CurrentUser current, ArtifactService service, CancellationToken ct) => await service.CreateAsync((await current.GetAsync(ct)).Id, request, ct)).WithName("CreateArtifact").Produces<ArtifactDto>();
        routes.MapGet("/{id:guid}", async (Guid id, int? version, CurrentUser current, ArtifactService service, CancellationToken ct) => await service.GetAsync((await current.GetAsync(ct)).Id, id, version, ct)).WithName("GetArtifact").Produces<ArtifactDto>();
        routes.MapPut("/{id:guid}", async (Guid id, SaveArtifactRequest request, CurrentUser current, ArtifactService service, CancellationToken ct) => await service.SaveAsync((await current.GetAsync(ct)).Id, id, request, ct)).WithName("SaveArtifact").Produces<ArtifactDto>();
        routes.MapDelete("/{id:guid}", async (Guid id, CurrentUser current, ArtifactService service, CancellationToken ct) => { await service.DeleteAsync((await current.GetAsync(ct)).Id, id, ct); return Results.NoContent(); }).WithName("DeleteArtifact").Produces(204);
        routes.MapGet("/{id:guid}/versions", async (Guid id, CurrentUser current, ArtifactService service, CancellationToken ct) => await service.RevisionsAsync((await current.GetAsync(ct)).Id, id, ct)).WithName("ArtifactVersions").Produces<IReadOnlyList<ArtifactRevisionDto>>();
        routes.MapGet("/{id:guid}/access", async (Guid id, CurrentUser current, ResourceAccess service, CancellationToken ct) => await service.AclAsync((await current.GetAsync(ct)).Id, id, "artifact", ct)).WithName("ArtifactAccess").Produces<ResourceAclDto>();
        routes.MapPut("/{id:guid}/access", async (Guid id, ResourceAclRequest request, CurrentUser current, ResourceAccess service, CancellationToken ct) => { await service.SetAclAsync((await current.GetAsync(ct)).Id, id, "artifact", request, ct); return Results.NoContent(); }).WithName("SaveArtifactAccess").Produces(204);
        routes.MapGet("/{id:guid}/export/{format}", async (Guid id, string format, int? version, CurrentUser current, ArtifactService service, ArtifactExport exporter, HttpContext http, CancellationToken ct) =>
        {
            var actor = (await current.GetAsync(ct)).Id; var artifact = await service.GetAsync(actor, id, version, ct);
            var output = await exporter.ExportAsync(artifact, format, ct);
            await service.GetAsync(actor, id, artifact.Version, ct); http.Response.Headers.CacheControl = "private, no-store";
            return Results.File(output.Data, output.ContentType, artifact.Resource.Name + "." + format);
        }).WithName("ExportArtifact");
        api.MapPost("/text/transform", async (TransformTextRequest request, CurrentUser current, TextTransformService service, CancellationToken ct) => await service.TransformAsync((await current.GetAsync(ct)).Id, request, ct)).RequireAuthorization("feature:text").WithTags("Artifacts").WithName("TransformText").Produces<TransformTextDto>();
    }
}
