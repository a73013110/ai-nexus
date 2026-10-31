using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Work.Artifacts;
using AiNexus.Features.Work.Files;
using AiNexus.Features.Admin.Audit;
using AiNexus.Features.Admin.Billing;
using AiNexus.Features.Work.Chat;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Work.Conversations;
using AiNexus.Features.Work.Dashboard;
using AiNexus.Features.Admin.Diagnostics;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Platform.Gd;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Admin.Mail;
using AiNexus.Features.Work.Jobs;
using AiNexus.Features.Work.Knowledge;
using AiNexus.Features.Work.Prompts;
using AiNexus.Features.Admin.Monitoring;
using AiNexus.Features.Platform.Notifications;
using AiNexus.Features.Platform.RemoteFiles;
using AiNexus.Features.Admin.Operations;
using AiNexus.Features.Admin.Organization;
using AiNexus.Features.Work.Projects;
using AiNexus.Features.Admin.Quality;
using AiNexus.Features.Admin.Releases;
using AiNexus.Features.Work.Repositories;
using AiNexus.Features.Office.Attendance;
using AiNexus.Features.Work.Sharing;
using AiNexus.Features.Platform.WebSearch;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features;

/// <summary>
/// 業務功能模組的註冊中心，系統中唯一完整列出所有模組的地方。
/// 新增模組時，在下面的 <c>AddFeatures</c> 與 <c>MapFeatures</c> 各加一行即可。
/// </summary>
public static class FeatureModules
{
    /// <summary>
    /// 註冊所有業務功能模組的服務。注意註冊順序會決定 <c>IEnumerable&lt;T&gt;</c> 的順序（例如儀表板區塊），調整時要留意。
    /// </summary>
    public static IHostApplicationBuilder AddFeatures(this IHostApplicationBuilder builder)
    {
        Add<AppDatabaseModule>(builder);
        Add<AccessControlModule>(builder);
        Add<IdentityModule>(builder);
        Add<OrganizationModule>(builder);
        Add<GdModule>(builder);
        Add<RemoteFilesModule>(builder);
        Add<AccountModule>(builder);
        Add<DiagnosticsModule>(builder);
        Add<BillingModule>(builder);
        Add<WebSearchModule>(builder);
        Add<RepositoriesModule>(builder);
        Add<DashboardModule>(builder);
        Add<OperationsModule>(builder);
        Add<QualityModule>(builder);
        Add<CollaborationModule>(builder);
        Add<NotificationsModule>(builder);
        Add<MailModule>(builder);
        Add<ReleasesModule>(builder);
        Add<ProjectsModule>(builder);
        Add<SharingModule>(builder);
        Add<KnowledgeModule>(builder);
        Add<ArtifactsModule>(builder);
        Add<JobsModule>(builder);
        Add<AuditModule>(builder);
        Add<ConversationsModule>(builder);
        Add<PromptsModule>(builder);
        Add<FilesModule>(builder);
        Add<InferenceModule>(builder);
        Add<ChatModule>(builder);
        Add<MonitoringModule>(builder);
        Add<AttendanceModule>(builder);

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
        // 登入由 fallback policy 預設要求，群組與端點都不必再宣告；只有功能 policy 與 AllowAnonymous 這類例外才寫在端點上。
        // 篩選器依此順序執行：解析目前使用者（身分驗證結果）、開始請求稽核，接著執行請求驗證（驗證失敗也留下稽核）。
        // 三者都在建置端點時判斷是否適用，不適用的端點不掛過濾器。
        var api = app.MapGroup("/api/v1")
            .WithSafeErrors()
            .WithCurrentUser()
            .WithAudit()
            .WithRequestValidation();

        Map<DiagnosticsModule>(api);
        Map<AccountModule>(api);
        Map<ConversationsModule>(api);
        Map<InferenceModule>(api);
        Map<ChatModule>(api);
        Map<FilesModule>(api);
        Map<PromptsModule>(api);
        Map<AuditModule>(api);
        Map<JobsModule>(api);
        Map<KnowledgeModule>(api);
        Map<CollaborationModule>(api);
        Map<ArtifactsModule>(api);
        Map<ProjectsModule>(api);
        Map<SharingModule>(api);
        Map<NotificationsModule>(api);
        Map<QualityModule>(api);
        Map<OperationsModule>(api);
        Map<IdentityModule>(api);
        Map<AccessControlModule>(api);
        Map<OrganizationModule>(api);
        Map<BillingModule>(api);
        Map<RepositoriesModule>(api);
        Map<DashboardModule>(api);
        Map<WebSearchModule>(api);
        Map<MonitoringModule>(api);
        Map<MailModule>(api);
        Map<ReleasesModule>(api);
        Map<AttendanceModule>(api);
        Map<RemoteFilesModule>(api);

        // 登入交握端點同樣走上面的篩選器；排在最後是為了維持既有的 OpenAPI 順序。
        api.MapGroup("").WithTags(Tag<IdentityModule>()).MapAuthenticationEndpoints();
        return app;
    }

    /// <summary>
    /// 啟動時檢查功能模組依賴的外部環境（目前是附件儲存目錄）。環境不健全時要在對外服務前中止，而不是等到第一次請求才失敗。
    /// </summary>
    public static void VerifyStartup(IServiceProvider services) => FilesModule.VerifyStorage(services);

    private static void Add<TModule>(IHostApplicationBuilder builder) where TModule : IFeatureModule => TModule.AddServices(builder);
    // OpenAPI tag 一律是模組名（類別名去掉 Module），端點自己不加 tag：管理端點分散在各模組，tag 跟著模組走才找得到。
    private static void Map<TModule>(RouteGroupBuilder api) where TModule : IFeatureModule => TModule.MapEndpoints(api.MapGroup("").WithTags(Tag<TModule>()));

    private static string Tag<TModule>() => typeof(TModule).Name[..^"Module".Length];
}
