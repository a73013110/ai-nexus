using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Integrations.Handoff;
using AiNexus.Features.Admin.Integrations.Sources;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Admin.Integrations;

public sealed class IntegrationsModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddSqlDatabase<GdwebDatabase>();
        services.AddSqlDatabase<MeihoDatabase>();
        services.AddSettings<IntegrationsOptions, IntegrationsOptionsValidator>(IntegrationsOptions.Section);
        services.AddScoped<SourceGateway>();
        services.AddMonitoringSource<IntegrationsMonitoring>();
        services.AddScoped<IControlledSourceAdapter, GdwebSource>();
        services.AddScoped<IControlledSourceAdapter, MeihoSource>();
        services.AddFeaturePolicy(FeatureCatalog.Integrations);
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/integrations").RequireAuthorization(Policies.Integrations);
        BrowseSources.Map(routes);
        ImportSourceRecord.Map(routes);
        StartSourceChat.Map(routes);
    }
}
