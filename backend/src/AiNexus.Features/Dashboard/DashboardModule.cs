using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Dashboard;

public sealed class DashboardModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<DashboardService>();
        builder.Services.AddFeaturePolicy("dashboard");
    }

    public static void MapEndpoints(RouteGroupBuilder api)
        => api.MapGet("/dashboard", async (string? scope, Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, CurrentUser current, DashboardService service, CancellationToken ct) =>
                Results.Ok(await service.GetAsync((await current.GetAsync(ct)).Id, scope ?? "personal", ownerId, from, until, offsetMinutes, ct)))
            .RequireAuthorization("feature:dashboard").WithName("GetDashboard").Produces<DashboardDto>();
}
