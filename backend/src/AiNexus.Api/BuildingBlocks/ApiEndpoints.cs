using AiNexus.Modules.Conversations;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Inference;
using AiNexus.Modules.Operations;

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
        api.MapOperations();
    }
}