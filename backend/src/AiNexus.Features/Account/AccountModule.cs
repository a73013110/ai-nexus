using AiNexus.Platform.Modules;

namespace AiNexus.Features.Account;

/// <summary>登入者本人的個人資料、偏好、設定與用量。</summary>
public sealed class AccountModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder) => builder.Services.AddScoped<PersonalUsage>();

    /// <summary>端點註冊順序即公開的 OpenAPI 順序，調整會改變對外文件。</summary>
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        GetMe.Map(api);
        UpdatePreferences.Map(api);
        GetUserSettings.Map(api);
        SaveUserSettings.Map(api);
        GetPersonalUsage.Map(api);
    }
}
