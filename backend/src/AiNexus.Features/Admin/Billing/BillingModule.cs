using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Billing.Prices;
using AiNexus.Features.Admin.Billing.Reports;
using AiNexus.Features.Platform.Inference.ModelTasks;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Admin.Billing;

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
        builder.Services.AddFeaturePolicy(FeatureCatalog.BillingPrices);
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        ReportSpend.MapPersonal(api);
        var prices = api.MapGroup("/admin/billing").RequireAuthorization(Policies.BillingPrices);
        ManagePrices.Map(prices);
        CreateModelPrice.Map(prices);
        // 全平台花費報表是總覽「全平台」範圍的匯出，與平台總覽同一個授權（GetDashboard）。
        ReportSpend.MapAdministrative(api.MapGroup("/admin/billing").RequireAuthorization(Policies.OperationsUsage));
    }
}
