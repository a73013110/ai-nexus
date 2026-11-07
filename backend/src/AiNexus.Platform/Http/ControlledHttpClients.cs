namespace AiNexus.Platform.Http;

/// <summary>
/// 對外連線用的受控 HTTP 客戶端，只給伺服器端設定的固定端點使用。
/// 停用自動重新導向，避免被帶到非預期的位址；停用預設請求日誌，避免機敏資訊外洩；逾時改由呼叫端自行控制。
/// </summary>
/// <remarks>
/// 主處理常式是 <see cref="SocketsHttpHandler"/>，連線池由它自己管理：連線最多存活 <see cref="PooledConnectionLifetime"/> 後重建，
/// DNS 變更因此會生效；處理常式本身不再由 IHttpClientFactory 每兩分鐘輪替（輪替會丟掉整個連線池、重新握手 TLS），
/// 所以高頻呼叫的外部服務（Google、模型供應商）共用同一組長連線。
/// 不開自動解壓縮：模型供應商的串流（SSE）若以 gzip 回應，解壓要等壓縮區塊湊滿才吐出，逐字串流會變成一段一段。
/// </remarks>
public static class ControlledHttpClients
{
    public const string Tools = "ControlledTools";

    /// <summary>連線存活上限；比 IIS 與多數負載平衡器的閒置逾時短，也讓 DNS 變更在數分鐘內生效。</summary>
    public static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(5);

    public static IHttpClientBuilder AddControlledHttpClient(this IServiceCollection services, string name)
        => services.AddHttpClient(name, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(CreateHandler)
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
            .RemoveAllLoggers();

    private static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        PooledConnectionLifetime = PooledConnectionLifetime,
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
        ConnectTimeout = TimeSpan.FromSeconds(15),
        EnableMultipleHttp2Connections = true,
    };
}
