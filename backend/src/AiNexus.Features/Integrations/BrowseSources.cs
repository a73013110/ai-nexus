using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Integrations;

/// <summary>Source list, search and record reading: read-only, never cached by the browser.</summary>
internal static class BrowseSources
{
    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapGet("", async (ICurrentUser user, SourceGateway gateway, CancellationToken ct) => Results.Ok(await gateway.SourcesAsync(user.Id, ct)))
            .Produces<IReadOnlyList<SourceDto>>();
        routes.MapGet("/{source}/records", async (string source, string query, string? kind, ICurrentUser user, SourceGateway gateway, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            return (await gateway.SearchAsync(user.Id, source, new(query, kind ?? "all"), ct)).ToHttpResult();
        }).Produces<IReadOnlyList<SourceRecordDto>>();
        routes.MapGet("/{source}/record", async (string source, string id, ICurrentUser user, SourceGateway gateway, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            return (await gateway.ReadAsync(user.Id, source, id, ct)).ToHttpResult();
        }).Produces<SourceDetailDto>();
    }
}
