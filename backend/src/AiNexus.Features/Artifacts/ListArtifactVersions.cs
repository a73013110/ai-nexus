using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Artifacts;

public sealed record ArtifactRevisionDto(int Version, string Title, string Author, DateTimeOffset CreatedAt);

/// <summary>Revision history, newest first, of an artifact the user may read.</summary>
internal sealed class ListArtifactVersions(NexusDbContext db, ResourceAccess access)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/versions", (Guid id, ICurrentUser user, ListArtifactVersions handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("ArtifactVersions");

    public async Task<Result<IReadOnlyList<ArtifactRevisionDto>>> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var readable = await access.RequireAsync(actor, id, Artifact.Kind, ct);
        if (!readable.IsSuccess) return readable.Error;
        return await (from revision in db.Set<ArtifactRevision>().AsNoTracking() join author in db.Users on revision.AuthorId equals author.Id
            where revision.ArtifactId == id orderby revision.Version descending
            select new ArtifactRevisionDto(revision.Version, revision.Title, author.DisplayName, revision.CreatedAt)).Take(Artifact.MaxVersions).ToListAsync(ct);
    }
}
