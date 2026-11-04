using AiNexus.Data.App;
using AiNexus.Data.App.Artifacts;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Artifacts.Management;

public sealed record ArtifactSummaryDto(ResourceDto Resource, int Version, Guid? ProjectId);

/// <summary>使用者可讀取、依更新時間排序的前 200 份成果，並標示每份是否可編輯。</summary>
internal sealed class ListArtifacts(AppDbContext db, ResourceAccess access) : IEndpointSlice
{
    private const int PageSize = 200;

    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("", (ICurrentUser user, ListArtifacts handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, ct))
        .Audited(ArtifactsAudit.ArtifactRead)
        .WithName("ListArtifacts");

    public async Task<IReadOnlyList<ArtifactSummaryDto>> HandleAsync(Guid actor, CancellationToken ct)
    {
        var rows = await (await access.QueryAsync(actor, Artifact.Kind, ct)).AsNoTracking()
            .Join(db.Artifacts.AsNoTracking(), resource => resource.Id, item => item.Id, (resource, item) => new { resource, item })
            .OrderByDescending(x => x.resource.UpdatedAt).Take(PageSize).ToListAsync(ct);
        // 可編輯與否交給 ResourceAccess 即時計算（含父專案編輯者），與單筆讀取的判斷一致。
        var described = await access.DescribeAllAsync(actor, [.. rows.Select(x => x.resource)], ct);
        return [.. rows.Zip(described, (row, resource) => new ArtifactSummaryDto(resource, row.item.Version, row.item.ProjectId))];
    }
}
