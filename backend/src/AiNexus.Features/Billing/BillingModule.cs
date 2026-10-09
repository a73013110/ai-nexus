using AiNexus.Features.AccessControl;
using AiNexus.Features.Inference;
using AiNexus.Platform.Modules;

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
        builder.Services.AddScoped<ManagePrices>();
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        ReportSpend.MapPersonal(api);
        var admin = api.MapGroup("/admin/billing").RequireAuthorization(Policies.Admin).WithTags("Billing");
        ManagePrices.Map(admin);
        ReportSpend.MapAdministrative(admin);
    }
}
