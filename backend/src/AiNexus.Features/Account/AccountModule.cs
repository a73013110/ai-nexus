using AiNexus.Platform.Modules;

namespace AiNexus.Features.Account;

/// <summary>The signed-in user's own profile, preferences, settings and usage. Each use case has its own file.</summary>
public sealed class AccountModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder) => builder.Services.AddScoped<PersonalUsage>();

    /// <summary>Endpoint order is the published OpenAPI order.</summary>
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        GetMe.Map(api);
        UpdatePreferences.Map(api);
        GetUserSettings.Map(api);
        SaveUserSettings.Map(api);
        GetPersonalUsage.Map(api);
    }
}
