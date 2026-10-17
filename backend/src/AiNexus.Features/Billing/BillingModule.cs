using AiNexus.Features.AccessControl;
using AiNexus.Features.Billing.Prices;
using AiNexus.Features.Billing.Reports;
using AiNexus.Features.Inference.ModelTasks;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Billing;

/// <summary>
/// 模型價格、計量費用、用量與花費報表。其他模組透過 <see cref="BillingService"/> 計量呼叫，並透過 <see cref="UsageReports"/> 讀取用量。
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
