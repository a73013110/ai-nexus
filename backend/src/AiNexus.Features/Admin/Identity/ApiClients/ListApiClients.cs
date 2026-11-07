using AiNexus.Data.App;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.ApiClients;

/// <summary>外部系統清單，依名稱排序。系統數以個位數計，不分頁。</summary>
internal sealed class ListApiClients(AppDbContext db) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("", (ListApiClients handler, CancellationToken ct) => handler.HandleAsync(ct))
        .Audited(IdentityAudit.ApiClientRead)
        .WithName("ListApiClients");

    public async Task<Result<ApiClientDto[]>> HandleAsync(CancellationToken ct)
        => await ApiClientDto.Project(db, db.ApiClients.AsNoTracking().OrderBy(x => x.Name)).ToArrayAsync(ct);
}
