using AiNexus.Features.AccessControl;
using AiNexus.Features.Account;
using AiNexus.Features.Administration;
using AiNexus.Features.Artifacts;
using AiNexus.Features.Attachments;
using AiNexus.Features.Billing;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Conversations;
using AiNexus.Features.Dashboard;
using AiNexus.Features.Diagnostics;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Integrations;
using AiNexus.Features.Knowledge;
using AiNexus.Features.Library;
using AiNexus.Features.Monitoring;
using AiNexus.Features.Notifications;
using AiNexus.Features.Operations;
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

/// <summary>The only place that lists modules. Adding a feature means one line in each method below.</summary>
public static class FeatureModules
{
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
        Add<OperationsModule>(builder);
        Add<ConversationsModule>(builder);
        Add<LibraryModule>(builder);
        Add<AttachmentsModule>(builder);
        Add<InferenceModule>(builder);
        Add<MonitoringModule>(builder);
        builder.Services.AddValidatorsFromAssembly(typeof(FeatureModules).Assembly, includeInternalTypes: true);
        return builder;
    }

    /// <summary>Endpoint order is the published OpenAPI order; keep it stable.</summary>
    public static WebApplication MapFeatures(this WebApplication app)
    {
        // Filters run in this order: resolve the user (authentication outcome), then validate the request.
        var api = app.MapGroup("/api/v1").RequireAuthorization().WithSafeErrors().WithCurrentUser().WithRequestValidation();
        Map<DiagnosticsModule>(api);
        Map<AccountModule>(api);
        Map<ConversationsModule>(api);
        Map<InferenceModule>(api);
        Map<AttachmentsModule>(api);
        Map<LibraryModule>(api);
        Map<OperationsModule>(api);
        Map<KnowledgeModule>(api);
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

    /// <summary>Resolves services whose construction validates machine state, so the host stops before serving traffic.</summary>
    public static void VerifyStartup(IServiceProvider services) => AttachmentsModule.VerifyStorage(services);

    private static void Add<TModule>(IHostApplicationBuilder builder) where TModule : IFeatureModule => TModule.AddServices(builder);
    private static void Map<TModule>(RouteGroupBuilder api) where TModule : IFeatureModule => TModule.MapEndpoints(api);
}
