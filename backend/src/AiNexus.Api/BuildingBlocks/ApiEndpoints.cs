using AiNexus.Modules.Conversations;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Inference;
using AiNexus.Modules.Operations;
using AiNexus.Modules.Attachments;
using AiNexus.Modules.Library;
using AiNexus.Modules.Administration;

namespace AiNexus.BuildingBlocks;

public static class ApiEndpoints
{
    public static void MapNexusApi(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization();
        api.ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404)
            .ProducesProblem(409).ProducesProblem(429).ProducesProblem(503);
        api.MapIdentity();
        api.MapConversations();
        api.MapInference();
        api.MapAttachments();
        api.MapPromptLibrary();
        api.MapOperations();
        api.MapJobs();
        AiNexus.Modules.Knowledge.KnowledgeEndpoints.MapKnowledge(api);
        AiNexus.Modules.Artifacts.ArtifactEndpoints.MapArtifacts(api);
        AiNexus.Modules.Projects.ProjectEndpoints.MapProjects(api);
        AiNexus.Modules.Sharing.ShareEndpoints.MapSharing(api);
        AiNexus.Modules.Quality.QualityEndpoints.MapQuality(api);
        AiNexus.Modules.Integrations.IntegrationEndpoints.MapIntegrations(api);
        api.MapAdministration();
        AiNexus.Modules.Billing.BillingEndpoints.MapBilling(api);
        AiNexus.Modules.Repositories.RepositoryEndpoints.MapRepositories(api);
        api.MapGet("/dashboard", async (string? scope, Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, CurrentUser current, AiNexus.Modules.Dashboard.DashboardService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync((await current.GetAsync(ct)).Id, scope ?? "personal", ownerId, from, until, offsetMinutes, ct)))
            .RequireAuthorization("feature:dashboard").WithName("GetDashboard").Produces<AiNexus.Modules.Dashboard.DashboardDto>();
        api.MapGet("/tools/web-search", (AiNexus.Modules.WebSearch.WebSearchService service) => Results.Ok(service.Status))
            .RequireAuthorization("feature:chat").WithName("GetWebSearchStatus").Produces<AiNexus.Modules.WebSearch.WebSearchStatusDto>();
    }
}
