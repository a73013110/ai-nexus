using System.Net;
using AiNexus.Platform.Http;
using AiNexus.Platform.Validation;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Platform.Google;

/// <summary>呼叫 Google API 的具名 HTTP 客戶端。</summary>
public static class GoogleApiClients
{
    /// <summary>Google API 的共同根網址；各 API 的路徑（<c>calendar/v3/…</c>）由客戶端自己加。</summary>
    public static readonly Uri ApiBase = new("https://www.googleapis.com/");

    /// <summary>
    /// 註冊呼叫 Google API 的具名客戶端：沿用受控客戶端的長連線池（<see cref="ControlledHttpClients"/>），所有 Google API 共用同樣的連線設定；
    /// 每個請求由 <see cref="GoogleAuthorizationHandler"/> 帶上 <paramref name="scope"/> 的存取權杖，代為操作的帳號取自 <see cref="GoogleOptions.DelegatedUser"/>。
    /// </summary>
    public static IHttpClientBuilder AddGoogleApiClient(this IServiceCollection services, string name, string scope)
        => services.AddControlledHttpClient(name)
            .ConfigureHttpClient(client => client.BaseAddress = ApiBase)
            .AddHttpMessageHandler(sp => new GoogleAuthorizationHandler(sp.GetRequiredService<GoogleAccessTokens>(), scope,
                sp.GetRequiredService<IOptions<GoogleOptions>>().Value.DelegatedUser.TrimToNull()));
}

/// <summary>替每個請求帶上存取權杖；API 回 401 時丟掉這個權杖，下一個請求重新換發（同一個請求不重送：內容可能已經讀過）。</summary>
internal sealed class GoogleAuthorizationHandler(GoogleAccessTokens tokens, string scope, string? subject) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokens.GetAsync(scope, subject, cancellationToken);
        request.Headers.Authorization = new("Bearer", token);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized) tokens.Invalidate(scope, subject, token);
        return response;
    }
}
