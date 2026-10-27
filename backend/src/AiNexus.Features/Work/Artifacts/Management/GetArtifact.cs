using AiNexus.Data.App;
using AiNexus.Data.App.Artifacts;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Artifacts.Management;

/// <summary>使用者可讀取的成果之單一版本，預設為目前版本。</summary>
internal sealed class GetArtifact(AppDbContext db, ResourceAccess access)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}", async (Guid id, int? version, ICurrentUser user, GetArtifact handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, version, ct)).ToHttpResult())
        .Audited(ArtifactsAudit.ArtifactRead)
        .WithName("GetArtifact");

    public async Task<Result<ArtifactDto>> HandleAsync(Guid actor, Guid id, int? version, CancellationToken ct)
    {
        var found = await access.RequireAsync(actor, id, Artifact.Kind, ct);
        if (!found.IsSuccess) return found.Error;
        var resource = found.Value;
        var item = await db.Set<Artifact>().AsNoTracking().SingleAsync(x => x.Id == id, ct);
        var wanted = version ?? item.Version;
        var revision = await db.Set<ArtifactRevision>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ArtifactId == id && x.Version == wanted, ct);
        if (revision is null) return ArtifactsErrors.VersionMissing;
        var info = await access.DescribeAsync(actor, resource, ct);
        return new ArtifactDto(info with { Name = revision.Title }, revision.Version, item.Version, revision.Content, item.SourceMessageId, item.ProjectId);
    }
}
