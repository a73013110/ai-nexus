using System.Net;
using AiNexus.Platform.Configuration;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace AiNexus.Platform.Http;

/// <summary>
/// 取得請求的真實來源位址。站台前方有反向代理時，連線位址都是代理本身，必須由受信任的代理轉送的標頭還原。
/// 稽核、日誌、線上人員與依位址限流都只透過 <see cref="ClientAddress"/> 取值，不自行讀 <c>Connection.RemoteIpAddress</c> 或標頭。
/// </summary>
public static class ReverseProxy
{
    public static IServiceCollection AddReverseProxy(this IServiceCollection services)
    {
        services.AddSettings<ReverseProxyOptions, ReverseProxyOptionsValidator>(ReverseProxyOptions.Section);
        services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<ReverseProxyOptions>>((options, proxy) =>
        {
            // 不轉送 Host：允許的主機由 AllowedHosts 管，讓代理改寫 Host 會繞過它。
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // 預設的 loopback 信任保留（同機的 ARR／out-of-process 代理），再加上設定的代理。
            foreach (var item in proxy.Value.TrustedProxies)
            {
                if (IpRangeItemsAttribute.TryParse(item, out var network)) options.KnownIPNetworks.Add(network);
            }
            // 多層代理時由右往左逐跳還原，遇到第一個不受信任的位址就停，所以不限跳數也不會採信客戶端自填的值。
            options.ForwardLimit = null;
        });
        return services;
    }

    /// <summary>必須是第一個中介軟體：之後的日誌、HTTPS 檢查、限流與稽核都依賴已還原的位址與 scheme。</summary>
    public static IApplicationBuilder UseReverseProxy(this IApplicationBuilder app) => app.UseForwardedHeaders();

    /// <summary>請求的來源位址；IPv4 對映的 IPv6（<c>::ffff:10.0.0.1</c>）還原成 IPv4，畫面與搜尋才是使用者熟悉的寫法。</summary>
    public static IPAddress? ClientAddress(this HttpContext http)
    {
        var address = http.Connection.RemoteIpAddress;
        return address?.IsIPv4MappedToIPv6 == true ? address.MapToIPv4() : address;
    }
}
