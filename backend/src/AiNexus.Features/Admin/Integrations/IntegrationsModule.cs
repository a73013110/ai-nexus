using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Integrations.Gd;
using AiNexus.Features.Admin.Integrations.Handoff;
using AiNexus.Features.Admin.Integrations.Sources;
using AiNexus.Features.Admin.Organization.Imports;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Admin.Integrations;

/// <summary>
/// 外部資料來源：受控來源的瀏覽與交給對話（<see cref="SourceGateway"/>），以及以程式查詢公文系統 GD 的共用存取
/// （<see cref="GdAccess"/>、帳號對應 <see cref="GdAccounts"/>、可用性監控 <c>gd</c>），出勤與組織匯入都經由它讀 GD。
/// </summary>
public sealed class IntegrationsModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddSqlDatabase<GdDatabase>();
        services.AddSqlDatabase<MeihoDatabase>();
        services.AddSettings<IntegrationsOptions, IntegrationsOptionsValidator>(IntegrationsOptions.Section);
        services.AddScoped<SourceGateway>();
        services.AddMonitoringSource<IntegrationsMonitoring>();
        services.AddScoped<IControlledSourceAdapter, GdSource>();
        services.AddScoped<IControlledSourceAdapter, MeihoSource>();
        services.AddSingleton<GdAccess>();
        services.AddScoped<GdAccounts>();
        services.AddMonitoringSource<GdMonitoring>();
        services.AddScoped<IUnitSource, GdUnitSource>();
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
