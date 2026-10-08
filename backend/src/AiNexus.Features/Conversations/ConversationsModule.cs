using AiNexus.Features.AccessControl;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Conversations;

/// <summary>The user's own conversations: list, organize, back up, branch and delete. Other modules use <see cref="ConversationService"/>; each use case has its own file.</summary>
public sealed class ConversationsModule : IFeatureModule
{
    /// <summary>Conversation backups may carry a whole message tree.</summary>
    public const long ImportBodyLimit = 8 * 1024 * 1024;

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddScoped<ConversationService>();
        services.AddScoped<GetConversation>();
        services.AddScoped<ImportConversation>();
        services.AddScoped<RenameConversation>();
        services.AddScoped<UpdateConversationSettings>();
        services.AddScoped<DuplicateConversation>();
        services.AddScoped<SelectBranch>();
        services.AddScoped<DeleteConversation>();
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
        GetConversation.Map(routes);
        RenameConversation.Map(routes);
        UpdateConversationSettings.Map(routes);
        DuplicateConversation.Map(routes);
        ExportConversation.Map(routes);
        SelectBranch.Map(routes);
        DeleteConversation.Map(routes);
    }
}
