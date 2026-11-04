using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Billing.Prices;
using AiNexus.Features.Admin.Billing.Reports;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.Billing;

/// <summary>
/// 模型價格、計量費用、用量與花費報表。其他模組透過 <see cref="BillingService"/> 計量呼叫，並透過 <see cref="UsageReports"/> 讀取用量。
/// </summary>
public sealed class BillingModule : IFeatureModule
{
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        api.MapSlice<ReportPersonalSpend>();
        api.MapGroup("/conversations").RequireFeature(FeatureCatalog.Chat)
            .MapSlice<ReportConversationSpend>();
        api.MapGroup("/admin/billing").RequireFeature(FeatureCatalog.BillingPrices)
            .MapSlice<ManagePrices>()
            .MapSlice<CreateModelPrice>();
        // 全平台花費報表是總覽「全平台」範圍的匯出，與平台總覽同一個授權（GetDashboard）。
        api.MapGroup("/admin/billing").RequireFeature(FeatureCatalog.OperationsUsage)
            .MapSlice<ReportAdministrativeSpend>();
    }
}
