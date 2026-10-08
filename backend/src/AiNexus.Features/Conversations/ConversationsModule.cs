using AiNexus.Features.AccessControl;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Conversations;

public sealed class ConversationsModule : IFeatureModule
{
    /// <summary>Conversation backups may carry a whole message tree.</summary>
    public const long ImportBodyLimit = 8 * 1024 * 1024;

    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<ConversationService>();
        builder.Services.AddScoped<ConversationOrganization>();
        builder.Services.AddFeaturePolicy(BuiltInAccess.ChatFeature);
    }

    public static void MapEndpoints(RouteGroupBuilder api) => api.MapConversations();
}
