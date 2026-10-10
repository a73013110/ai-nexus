using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Integrations;

/// <summary>Source list, search and record reading: read-only, never cached by the browser.</summary>
internal sealed class BrowseSources(SourceGateway gateway)
{
    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapGet("", async (ICurrentUser user, BrowseSources handler, CancellationToken ct) => TypedResults.Ok(await handler.ListAsync(user.Id, ct)));
        routes.MapGet("/{source}/records", (string source, string query, string? kind, ICurrentUser user, BrowseSources handler, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            return handler.SearchAsync(user.Id, source, new(query, kind ?? "all"), ct).ToHttpResultAsync();
        });
        routes.MapGet("/{source}/record", (string source, string id, ICurrentUser user, BrowseSources handler, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            return handler.ReadAsync(user.Id, source, id, ct).ToHttpResultAsync();
        });
    }

    public Task<IReadOnlyList<SourceDto>> ListAsync(Guid actor, CancellationToken ct) => gateway.SourcesAsync(actor, ct);

    public Task<Result<IReadOnlyList<SourceRecordDto>>> SearchAsync(Guid actor, string source, SourceSearchRequest request, CancellationToken ct) => gateway.SearchAsync(actor, source, request, ct);

    public Task<Result<SourceDetailDto>> ReadAsync(Guid actor, string source, string id, CancellationToken ct) => gateway.ReadAsync(actor, source, id, ct);
}
