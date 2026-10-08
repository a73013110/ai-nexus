using AiNexus.Modules.Conversations;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Inference;
using AiNexus.Modules.Operations;
using AiNexus.Modules.Attachments;
using AiNexus.Modules.Library;
using AiNexus.Modules.Administration;

using AiNexus.BuildingBlocks.Diagnostics;
namespace AiNexus.BuildingBlocks;

public static class ApiEndpoints
{
    public static void MapNexusApi(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization().WithSafeErrors();
        api.MapDiagnostics();
        api.MapIdentity();
        api.MapConversations();
        api.MapInference();
        api.MapAttachments();
        api.MapPromptLibrary();
        api.MapOperations();
        api.MapActivityAudit();
        api.MapJobs();
        AiNexus.Modules.Knowledge.KnowledgeEndpoints.MapKnowledge(api);
        AiNexus.Modules.Artifacts.ArtifactEndpoints.MapArtifacts(api);
        AiNexus.Modules.Projects.ProjectEndpoints.MapProjects(api);
        AiNexus.Modules.Sharing.ShareEndpoints.MapSharing(api);
        AiNexus.Modules.Notifications.NotificationEndpoints.MapNotifications(api);
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
        AiNexus.Modules.Monitoring.MonitoringEndpoints.MapMonitoring(api);
    }
}
