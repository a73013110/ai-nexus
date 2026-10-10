using AiNexus.Features.AccessControl;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Configuration;

namespace AiNexus.Features.Integrations;

public sealed class IntegrationsModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddSqlDatabase<LegacyGdwebDatabase>();
        services.AddSqlDatabase<LegacyMeihoDatabase>();
        services.AddSettings<IntegrationsOptions, IntegrationsOptionsValidator>(IntegrationsOptions.Section);
        services.AddScoped<SourceGateway>();
        services.AddScoped<IControlledSourceAdapter, GdwebSource>();
        services.AddScoped<IControlledSourceAdapter, MeihoSource>();
        services.AddFeaturePolicy(FeatureIds.Integrations);
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/integrations").RequireAuthorization(Policies.Integrations).WithTags("Integrations");
        BrowseSources.Map(routes);
        ImportSourceRecord.Map(routes);
        StartSourceChat.Map(routes);
    }
}

internal sealed class IntegrationsFeatures() : FeatureSeed(new PlatformFeature(FeatureIds.Integrations, "資料來源", "/integrations", 80, AdministratorsOnly: true));
