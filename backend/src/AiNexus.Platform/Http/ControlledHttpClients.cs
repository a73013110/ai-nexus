namespace AiNexus.Platform.Http;

/// <summary>Outbound clients for server-configured endpoints: no redirects, no request logging, caller-owned timeouts.</summary>
public static class ControlledHttpClients
{
    public const string Tools = "ControlledTools";

    public static IHttpClientBuilder AddControlledHttpClient(this IServiceCollection services, string name)
        => services.AddHttpClient(name, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).RemoveAllLoggers();
}
