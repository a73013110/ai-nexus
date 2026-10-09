using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Artifacts;

/// <summary>One version (the current one by default) of an artifact the user may read.</summary>
internal sealed class GetArtifact(NexusDbContext db, ResourceAccess access)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}", async (Guid id, int? version, ICurrentUser user, GetArtifact handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, version, ct)).ToHttpResult())
        .WithName("GetArtifact").Produces<ArtifactDto>();

    public async Task<Result<ArtifactDto>> HandleAsync(Guid actor, Guid id, int? version, CancellationToken ct)
    {
        var resource = await access.RequireAsync(actor, id, Artifact.Kind, ct);
        var item = await db.Set<Artifact>().AsNoTracking().SingleAsync(x => x.Id == id, ct);
        var revision = await db.Set<ArtifactRevision>().AsNoTracking().SingleOrDefaultAsync(x => x.ArtifactId == id && x.Version == (version ?? item.Version), ct);
        if (revision is null) return ArtifactsErrors.VersionMissing;
        var info = await access.DescribeAsync(actor, resource, ct);
        return new ArtifactDto(info with { Name = revision.Title }, revision.Version, item.Version, revision.Content, item.SourceMessageId, item.ProjectId);
    }
}
