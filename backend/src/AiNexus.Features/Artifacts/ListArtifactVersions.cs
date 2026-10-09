using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Artifacts;

public sealed record ArtifactRevisionDto(int Version, string Title, string Author, DateTimeOffset CreatedAt);

/// <summary>Revision history, newest first, of an artifact the user may read.</summary>
internal static class ListArtifactVersions
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/versions", async (Guid id, ICurrentUser user, NexusDbContext db, ResourceAccess access, CancellationToken ct) =>
            Results.Ok(await HandleAsync(db, access, user.Id, id, ct)))
        .WithName("ArtifactVersions").Produces<IReadOnlyList<ArtifactRevisionDto>>();

    public static async Task<IReadOnlyList<ArtifactRevisionDto>> HandleAsync(NexusDbContext db, ResourceAccess access, Guid actor, Guid id, CancellationToken ct)
    {
        await access.RequireAsync(actor, id, Artifact.Kind, ct);
        return await (from revision in db.Set<ArtifactRevision>().AsNoTracking() join author in db.Users on revision.AuthorId equals author.Id
            where revision.ArtifactId == id orderby revision.Version descending
            select new ArtifactRevisionDto(revision.Version, revision.Title, author.DisplayName, revision.CreatedAt)).Take(Artifact.MaxVersions).ToListAsync(ct);
    }
}
