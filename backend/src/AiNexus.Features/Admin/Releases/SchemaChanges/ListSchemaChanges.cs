using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.Releases.SchemaChanges;

/// <summary>版本頁的異動清單：每次開啟即時計算，不用監控的快取，回收到開發庫之後馬上看得到結果。</summary>
internal sealed class ListSchemaChanges(SchemaChangeReader changes) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("", (ListSchemaChanges handler, CancellationToken ct) => handler.HandleAsync(ct))
        .Audited(ReleasesAudit.SchemaChangesRead)
        .WithName("ListSchemaChanges");

    public Task<IReadOnlyList<SchemaChangeDto>> HandleAsync(CancellationToken ct) => changes.ListAsync(ct);
}
