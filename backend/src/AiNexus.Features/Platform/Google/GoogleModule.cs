using AiNexus.Features.Platform.Google.Calendar;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Platform.Google;

/// <summary>
/// Google API：所有模組共用的服務帳戶（<see cref="GoogleServiceAccount"/>）與權杖快取（<see cref="GoogleAccessTokens"/>），
/// 以及各 API 的客戶端（目前是日曆 <see cref="IGoogleCalendar"/>）。業務規則（同步什麼、多久一次）寫在使用端，不放這裡。
/// 新增一種 Google API：以 <see cref="GoogleApiClients.AddGoogleApiClient"/> 註冊具名客戶端並給它需要的範圍，回應交給 <see cref="GoogleHttp"/> 分類。
/// </summary>
public sealed class GoogleModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddSettings<GoogleOptions, GoogleOptionsValidator>(GoogleOptions.Section);
        builder.Services.AddGoogleApiClient(GoogleCalendarClient.HttpClientName, GoogleCalendarClient.Scope);
    }
}
