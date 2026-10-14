using AiNexus.Features.AccessControl;
using AiNexus.Features.Billing.Prices;
using AiNexus.Features.Billing.Reports;
using AiNexus.Features.Inference.ModelTasks;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Billing;

/// <summary>
/// Model prices, metered charges, usage and spend reports. Other modules meter calls through <see cref="BillingService"/>
/// and read usage through <see cref="UsageReports"/>.
/// </summary>
public sealed class BillingModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<BillingService>();
        builder.Services.AddScoped<IModelCallMeter>(sp => sp.GetRequiredService<BillingService>());
        builder.Services.AddScoped<SpendReports>();
        builder.Services.AddScoped<UsageReports>();
        builder.Services.AddMonitoringSource<UsageMonitoring>();
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        ReportSpend.MapPersonal(api);
        var admin = api.MapGroup("/admin/billing").RequireAuthorization(Policies.Operations).WithTags("Billing");
        ManagePrices.Map(admin);
        ReportSpend.MapAdministrative(admin);
    }
}
