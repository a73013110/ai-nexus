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
    }
}
