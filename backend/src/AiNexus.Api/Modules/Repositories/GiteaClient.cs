using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AiNexus.BuildingBlocks;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Repositories;

public interface IGiteaClient
{
    Task<JsonDocument> GetAsync(string token, string path, CancellationToken ct);
}
public sealed class GiteaClient(IHttpClientFactory clients, IOptions<GiteaOptions> options) : IGiteaClient
{
    public async Task<JsonDocument> GetAsync(string token, string path, CancellationToken ct)
    {
        // There is deliberately no mutation method and no user-supplied host/download URL.
        if (!path.StartsWith("api/v1/", StringComparison.Ordinal)) throw new InvalidOperationException("Controlled Gitea path required.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        var endpoint = new Uri(new Uri(options.Value.BaseUrl.TrimEnd('/') + "/"), path);
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("token", token); request.Headers.Accept.ParseAdd("application/json");
        try
        {
            using var response = await clients.CreateClient("ControlledTools").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) throw new ApiException(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? 403 : response.StatusCode == HttpStatusCode.NotFound ? 404 : 503,
                "gitea_read_failed", response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? "Gitea 授權不足或 token 已失效，請重新連線並確認唯讀權限。" : "Gitea 項目無法讀取，請確認路徑、版本與權限。");
            return await BoundedHttpJson.ReadAsync(response, Math.Max(1024 * 1024, options.Value.MaxFileBytes * 2), timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ApiException(504, "gitea_timeout", "Gitea 讀取逾時。"); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException) { throw new ApiException(503, "gitea_unavailable", "Gitea 目前無法使用，請稍後重試。"); }
    }
}
