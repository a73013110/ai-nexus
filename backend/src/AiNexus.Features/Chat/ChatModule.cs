using AiNexus.Features.AccessControl;
using AiNexus.Features.Chat.Context;
using AiNexus.Features.Chat.Runs;
using AiNexus.Features.Chat.Transcripts;
using AiNexus.Features.Identity;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Chat;

/// <summary>對話工作區功能模組：上下文建置、生成與 SSE 串流、對話生命週期（查詢、複製、匯出、刪除）。</summary>
public sealed class ChatModule : IFeatureModule
{
    /// <summary>所有會觸發生成的請求（新發問、重新生成、編輯發問、網路搜尋）共用的限流原則。</summary>
    public const string SendRateLimit = "chat-send";

    /// <summary>每位使用者每分鐘最多發送 30 次生成請求，防止惡意腳本轟炸引發全域排隊延遲。</summary>
    public const int SendsPerMinute = 30;

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddDashboardSource<ChatDashboard>();
        services.AddScoped<RunService>();
        services.AddScoped<RunLeaseRecovery>();
        services.AddScoped<ContextBuilder>();
        services.AddSingleton<SubscriptionLimits>();
        services.AddSingleton<RunSignals>();

        services.AddHostedService<GenerationWorker>();
        services.AddHostedService<RunRecoveryWorker>();
        services.AddHostedService<RunEventRetentionWorker>();

        services.AddRateLimiter(options => options.AddPerUserLimit(SendRateLimit, SendsPerMinute));
    }

    /// <summary>註冊對話相關之 HTTP 端點。</summary>
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var runs = api.MapGroup("").RequireAuthorization(Policies.Chat).WithTags("Inference");
        PreviewContext.Map(runs);
        CreateRun.Map(runs);
        GetRun.Map(runs);
        CancelRun.Map(runs);
        StreamRunEvents.Map(runs);

        var conversations = api.MapGroup("/conversations").RequireAuthorization(Policies.Chat).WithTags("Conversations");
        GetConversation.Map(conversations);
        DuplicateConversation.Map(conversations);
        ExportConversation.Map(conversations);
        DeleteConversation.Map(conversations);
    }
}
