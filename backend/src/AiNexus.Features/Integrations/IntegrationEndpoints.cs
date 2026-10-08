using AiNexus.Features.Identity;
using AiNexus.Features.Artifacts;
namespace AiNexus.Features.Integrations;
public static class IntegrationEndpoints
{
    public static void MapIntegrations(this RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/integrations").RequireAuthorization("feature:integrations").WithTags("Integrations");
        routes.MapGet("", async (CurrentUser u, IntegrationService s, CancellationToken ct) => Results.Ok(await s.SourcesAsync((await u.GetAsync(ct)).Id, ct))).Produces<IReadOnlyList<SourceDto>>();
        routes.MapGet("/{source}/records", async (string source, string query, string? kind, CurrentUser u, IntegrationService s, HttpContext http, CancellationToken ct) => { http.Response.Headers.CacheControl = "private, no-store"; return Results.Ok(await s.SearchAsync((await u.GetAsync(ct)).Id, source, new(query, kind ?? "all"), ct)); }).Produces<IReadOnlyList<SourceRecordDto>>();
        routes.MapGet("/{source}/record", async (string source, string id, CurrentUser u, IntegrationService s, HttpContext http, CancellationToken ct) => { http.Response.Headers.CacheControl = "private, no-store"; return Results.Ok(await s.ReadAsync((await u.GetAsync(ct)).Id, source, id, ct)); }).Produces<SourceDetailDto>();
        routes.MapPost("/{source}/import", async (string source, SourceImportRequest body, CurrentUser u, IntegrationService s, CancellationToken ct) => Results.Ok(await s.ImportAsync((await u.GetAsync(ct)).Id, source, body, ct))).Produces<ArtifactDto>();
        routes.MapPost("/{source}/chat", async (string source, SourceImportRequest body, CurrentUser u, IntegrationService s, CancellationToken ct) => Results.Ok(await s.PrepareChatAsync((await u.GetAsync(ct)).Id, source, body, ct))).Produces<SourceChatDto>();
    }
}
