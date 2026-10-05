using AiNexus.Modules.Identity;
using AiNexus.BuildingBlocks;
namespace AiNexus.Modules.Sharing;
public static class ShareEndpoints
{
    public static void MapSharing(this RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/shares").RequireAuthorization("feature:shared").WithTags("Sharing");
        routes.MapGet("", async (bool? sent, CurrentUser u, ShareService s, CancellationToken ct) => Results.Ok(await s.ListAsync((await u.GetAsync(ct)).Id, sent ?? false, ct))).Produces<IReadOnlyList<ShareDto>>();
        routes.MapPost("", async (CreateShareRequest body, CurrentUser u, ShareService s, CancellationToken ct) => Results.Ok(await s.CreateAsync((await u.GetAsync(ct)).Id, body, ct))).Produces<ShareDto>();
        routes.MapGet("/{id:guid}", async (Guid id, CurrentUser u, ShareService s, CancellationToken ct) => Results.Ok(await s.ReadAsync((await u.GetAsync(ct)).Id, id, ct))).Produces<SharedContentDto>();
        routes.MapDelete("/{id:guid}", async (Guid id, CurrentUser u, ShareService s, CancellationToken ct) => { await s.RevokeAsync((await u.GetAsync(ct)).Id, id, ct); return Results.NoContent(); });
        routes.MapGet("/{id:guid}/files/{file:guid}", async (Guid id, Guid file, CurrentUser u, ShareService s, HttpContext http, CancellationToken ct) => { var data = await s.FileAsync((await u.GetAsync(ct)).Id, id, file, ct); return WebSecurity.File(http, data.Data, data.ContentType, data.FileName, download: true); });
    }
}
