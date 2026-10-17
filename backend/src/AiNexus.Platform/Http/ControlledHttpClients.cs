namespace AiNexus.Platform.Http;

/// <summary>對外連線受控 HTTP 客戶端：用於伺服器端配置的特定端點，停用自動重新導向、停用預設請求日誌（避免機敏資訊外洩），並由呼叫端精確掌控逾時（Timeout）。</summary>
public static class ControlledHttpClients
{
    public const string Tools = "ControlledTools";

    public static IHttpClientBuilder AddControlledHttpClient(this IServiceCollection services, string name)
        => services.AddHttpClient(name, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).RemoveAllLoggers();
}
