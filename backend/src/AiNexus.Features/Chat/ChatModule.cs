using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Chat;

/// <summary>
/// The chat workspace: builds a turn's context from the conversation, attachments, project, knowledge and web search,
/// runs and streams the answer, and reads, exports, duplicates or deletes a whole conversation with everything attached
/// to it. It sits above the modules it combines; each use case has its own file.
/// </summary>
public sealed class ChatModule : IFeatureModule
{
    /// <summary>
    /// Sending (including regenerate, edit and web search, which are runs too). Above any human pace; it stops scripts
    /// from turning one user's quota checks, context building and provider queue into a load on everyone.
    /// </summary>
    public const string SendRateLimit = "chat-send";
    public const int SendsPerMinute = 30;

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddScoped<RunService>();
        services.AddScoped<PreviewContext>();
        services.AddScoped<CreateRun>();
        services.AddScoped<CancelRun>();
        services.AddScoped<RunLeaseRecovery>();
        services.AddScoped<ContextBuilder>();
        services.AddScoped<GetConversation>();
        services.AddScoped<DuplicateConversation>();
        services.AddScoped<DeleteConversation>();
        services.AddSingleton<SubscriptionLimits>();
        services.AddSingleton<RunSignals>();
        services.AddHostedService<GenerationWorker>();
        services.AddHostedService<RunRecoveryWorker>();
        services.AddRateLimiter(options => options.AddPerUserLimit(SendRateLimit, SendsPerMinute));
    }

    // Endpoint order is the published OpenAPI order; the endpoints keep the tags of the API sections they belong to.
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var runs = api.MapGroup("").RequireAuthorization(Policies.Chat).WithTags("Inference");
        PreviewContext.Map(runs);
        CreateRun.Map(runs);
        GetRun.Map(runs);
        CancelRun.Map(runs);
        RunEventsEndpoint.Map(runs);
        var conversations = api.MapGroup("/conversations").RequireAuthorization(Policies.Chat).WithTags("Conversations");
        GetConversation.Map(conversations);
        DuplicateConversation.Map(conversations);
        ExportConversation.Map(conversations);
        DeleteConversation.Map(conversations);
    }
}
