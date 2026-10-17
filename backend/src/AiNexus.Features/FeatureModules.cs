using AiNexus.Features.AccessControl;
using AiNexus.Features.Account;
using AiNexus.Features.Administration;
using AiNexus.Features.Artifacts;
using AiNexus.Features.Attachments;
using AiNexus.Features.Audit;
using AiNexus.Features.Billing;
using AiNexus.Features.Chat;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Conversations;
using AiNexus.Features.Dashboard;
using AiNexus.Features.Diagnostics;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Integrations;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge;
using AiNexus.Features.Library;
using AiNexus.Features.Monitoring;
using AiNexus.Features.Notifications;
using AiNexus.Features.Persistence;
using AiNexus.Features.Projects;
using AiNexus.Features.Quality;
using AiNexus.Features.Repositories;
using AiNexus.Features.Sharing;
using AiNexus.Features.WebSearch;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features;

/// <summary>
/// 應用程式功能模組集中宣告與註冊中心。
/// 系統中唯一完整列出所有業務功能模組的地方；新增功能模組時僅需在以下各方法新增一行註冊。
/// </summary>
public static class FeatureModules
{
    /// <summary>
    /// 註冊所有業務功能模組的服務。注意註冊順序會決定 <c>IEnumerable&lt;T&gt;</c> 的順序（例如儀表板區塊），調整時要留意。
    /// </summary>
    public static IHostApplicationBuilder AddFeatures(this IHostApplicationBuilder builder)
    {
        Add<PersistenceModule>(builder);
        Add<AccessControlModule>(builder);
        Add<IdentityModule>(builder);
        Add<AccountModule>(builder);
        Add<DiagnosticsModule>(builder);
        Add<BillingModule>(builder);
        Add<WebSearchModule>(builder);
        Add<RepositoriesModule>(builder);
        Add<DashboardModule>(builder);
        Add<AdministrationModule>(builder);
        Add<QualityModule>(builder);
        Add<IntegrationsModule>(builder);
        Add<CollaborationModule>(builder);
        Add<NotificationsModule>(builder);
        Add<ProjectsModule>(builder);
        Add<SharingModule>(builder);
        Add<KnowledgeModule>(builder);
        Add<ArtifactsModule>(builder);
        Add<JobsModule>(builder);
        Add<AuditModule>(builder);
        Add<ConversationsModule>(builder);
        Add<LibraryModule>(builder);
        Add<AttachmentsModule>(builder);
        Add<InferenceModule>(builder);
        Add<ChatModule>(builder);
        Add<MonitoringModule>(builder);

        builder.Services.AddEndpointHandlers(typeof(FeatureModules).Assembly);
        builder.Services.AddValidatorsFromAssembly(typeof(FeatureModules).Assembly, includeInternalTypes: true);
        return builder;
    }

    /// <summary>
    /// 將各業務模組的 API 端點對應至路由群組。
    /// 端點註冊順序即為公開的 OpenAPI 文件順序，請保持穩定。
    /// </summary>
    public static WebApplication MapFeatures(this WebApplication app)
    {
        // 篩選器依此順序執行：解析目前使用者（身分驗證結果），接著執行請求驗證。
        var api = app.MapGroup("/api/v1")
            .RequireAuthorization()
            .WithSafeErrors()
            .WithCurrentUser()
            .WithRequestValidation();

        Map<DiagnosticsModule>(api);
        Map<AccountModule>(api);
        Map<ConversationsModule>(api);
        Map<InferenceModule>(api);
        Map<ChatModule>(api);
        Map<AttachmentsModule>(api);
        Map<LibraryModule>(api);
        Map<AuditModule>(api);
        Map<JobsModule>(api);
        Map<KnowledgeModule>(api);
        Map<CollaborationModule>(api);
        Map<ArtifactsModule>(api);
        Map<ProjectsModule>(api);
        Map<SharingModule>(api);
        Map<NotificationsModule>(api);
        Map<QualityModule>(api);
        Map<IntegrationsModule>(api);
        Map<AdministrationModule>(api);
        Map<BillingModule>(api);
        Map<RepositoriesModule>(api);
        Map<DashboardModule>(api);
        Map<WebSearchModule>(api);
        Map<MonitoringModule>(api);

        IdentityModule.MapPublicEndpoints(app);
        return app;
    }

    /// <summary>
    /// 在主機啟動前解析並驗證關鍵實體狀態（如附件磁碟目錄），若環境不健全則在對外提供服務前立即中止。
    /// </summary>
    public static void VerifyStartup(IServiceProvider services) => AttachmentsModule.VerifyStorage(services);

    private static void Add<TModule>(IHostApplicationBuilder builder) where TModule : IFeatureModule => TModule.AddServices(builder);
    private static void Map<TModule>(RouteGroupBuilder api) where TModule : IFeatureModule => TModule.MapEndpoints(api);
}
