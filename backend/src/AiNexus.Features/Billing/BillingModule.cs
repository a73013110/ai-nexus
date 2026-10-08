using AiNexus.Features.Administration;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Billing;

/// <summary>Model prices, metered charges and spend reports. Other modules meter calls through <see cref="BillingService"/>.</summary>
public sealed class BillingModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<BillingService>();
        builder.Services.AddScoped<SpendReports>();
        builder.Services.AddScoped<ManagePrices>();
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        ReportSpend.MapPersonal(api);
        var admin = api.MapGroup("/admin/billing").RequireAuthorization(AdministrationConfiguration.Policy).WithTags("Billing");
        ManagePrices.Map(admin);
        ReportSpend.MapAdministrative(admin);
    }
}
