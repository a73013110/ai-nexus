using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Operations;
using AiNexus.Features.Attachments;
using AiNexus.Features.Library;
using AiNexus.Features.Administration;

using AiNexus.Platform.Diagnostics;
using AiNexus.Features.Diagnostics;
namespace AiNexus.Features;

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
        api.MapJobs();
        AiNexus.Features.Knowledge.KnowledgeEndpoints.MapKnowledge(api);
        AiNexus.Features.Artifacts.ArtifactEndpoints.MapArtifacts(api);
        AiNexus.Features.Projects.ProjectEndpoints.MapProjects(api);
        AiNexus.Features.Sharing.ShareEndpoints.MapSharing(api);
        AiNexus.Features.Notifications.NotificationEndpoints.MapNotifications(api);
        AiNexus.Features.Quality.QualityEndpoints.MapQuality(api);
        AiNexus.Features.Integrations.IntegrationEndpoints.MapIntegrations(api);
        api.MapAdministration();
        AiNexus.Features.Billing.BillingEndpoints.MapBilling(api);
        AiNexus.Features.Repositories.RepositoryEndpoints.MapRepositories(api);
        api.MapGet("/dashboard", async (string? scope, Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, CurrentUser current, AiNexus.Features.Dashboard.DashboardService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync((await current.GetAsync(ct)).Id, scope ?? "personal", ownerId, from, until, offsetMinutes, ct)))
            .RequireAuthorization("feature:dashboard").WithName("GetDashboard").Produces<AiNexus.Features.Dashboard.DashboardDto>();
        api.MapGet("/tools/web-search", (AiNexus.Features.WebSearch.WebSearchService service) => Results.Ok(service.Status))
            .RequireAuthorization("feature:chat").WithName("GetWebSearchStatus").Produces<AiNexus.Features.WebSearch.WebSearchStatusDto>();
    }
}
