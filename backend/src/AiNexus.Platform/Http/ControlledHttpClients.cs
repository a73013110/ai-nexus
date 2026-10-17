namespace AiNexus.Platform.Http;

/// <summary>
/// 對外連線用的受控 HTTP 客戶端，只給伺服器端設定的固定端點使用。
/// 停用自動重新導向，避免被帶到非預期的位址；停用預設請求日誌，避免機敏資訊外洩；逾時改由呼叫端自行控制。
/// </summary>
public static class ControlledHttpClients
{
    public const string Tools = "ControlledTools";

    public static IHttpClientBuilder AddControlledHttpClient(this IServiceCollection services, string name)
        => services.AddHttpClient(name, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).RemoveAllLoggers();
}
