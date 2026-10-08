using AiNexus.Platform.Modules;

namespace AiNexus.Features.Billing;

public sealed class BillingModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<BillingService>();
        builder.Services.AddScoped<SpendReports>();
    }

    public static void MapEndpoints(RouteGroupBuilder api) => BillingEndpoints.MapBilling(api);
}
