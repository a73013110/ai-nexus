using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Integrations.Handoff;
using AiNexus.Features.Admin.Integrations.Sources;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Admin.Integrations;

/// <summary>
/// 外部資料來源：受控來源的瀏覽與交給對話（<see cref="SourceGateway"/>）。GD 的連線由 Gd 模組宣告（<c>Databases:Gd</c>）。
/// </summary>
public sealed class IntegrationsModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddSqlDatabase<MeihoDatabase>();
        services.AddDatabase(MeihoDatabase.Name, readOnly: true, label: "校務資料庫");
        services.AddSettings<IntegrationsOptions, IntegrationsOptionsValidator>(IntegrationsOptions.Section);
        services.AddScoped<SourceGateway>();
        services.AddMonitoringSource<IntegrationsMonitoring>();
        services.AddScoped<IControlledSourceAdapter, GdSource>();
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
