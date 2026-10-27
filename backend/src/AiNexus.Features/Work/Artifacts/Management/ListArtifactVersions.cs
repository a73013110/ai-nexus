using AiNexus.Data.App;
using AiNexus.Data.App.Artifacts;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Artifacts.Management;

public sealed record ArtifactRevisionDto(int Version, string Title, string Author, DateTimeOffset CreatedAt);

/// <summary>使用者可讀取的成果之版本歷程，由新到舊。</summary>
internal sealed class ListArtifactVersions(AppDbContext db, ResourceAccess access)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/versions", (Guid id, ICurrentUser user, ListArtifactVersions handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .Audited(ArtifactsAudit.VersionRead)
        .WithName("ArtifactVersions");

    public async Task<Result<IReadOnlyList<ArtifactRevisionDto>>> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var readable = await access.RequireAsync(actor, id, Artifact.Kind, ct);
        if (!readable.IsSuccess) return readable.Error;
        return await (from revision in db.Set<ArtifactRevision>().AsNoTracking()
                join author in db.Users on revision.AuthorId equals author.Id
                where revision.ArtifactId == id
                orderby revision.Version descending
                select new ArtifactRevisionDto(revision.Version, revision.Title, author.DisplayName, revision.CreatedAt))
            .Take(Artifact.MaxVersions)
            .ToListAsync(ct);
    }
}
