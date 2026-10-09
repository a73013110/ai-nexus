using AiNexus.Features.AccessControl;
using AiNexus.Features.Collaboration;
using AiNexus.Platform.Events;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Conversations;

/// <summary>
/// The user's own conversations and messages: list, create, organize, import and branch. Other modules use
/// <see cref="ConversationService"/>; each use case has its own file.
/// </summary>
public sealed class ConversationsModule : IFeatureModule
{
    /// <summary>Conversation backups may carry a whole message tree.</summary>
    public const long ImportBodyLimit = 8 * 1024 * 1024;

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddScoped<ConversationService>();
        services.AddScoped<ImportConversation>();
        services.AddScoped<RenameConversation>();
        services.AddScoped<UpdateConversationSettings>();
        services.AddScoped<SelectBranch>();
        services.AddDomainEventHandler<ContainerDeleted, DetachDeletedProjectConversations>();
        services.AddFeaturePolicy(FeatureIds.Chat);
    }

    // Endpoint order is the published OpenAPI order.
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/conversations").RequireAuthorization(Policies.Chat).WithTags("Conversations");
        ListConversations.Map(routes);
        ListConversationLabels.Map(routes);
        ImportConversation.Map(routes);
        CreateConversation.Map(routes);
        RenameConversation.Map(routes);
        UpdateConversationSettings.Map(routes);
        SelectBranch.Map(routes);
    }
}
