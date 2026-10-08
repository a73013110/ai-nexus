using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Repositories;

public interface IGiteaClient
{
    Task<JsonDocument> GetAsync(string token, string path, CancellationToken ct);
    Task<string> GetTextAsync(string token, string path, CancellationToken ct);
}
public sealed class GiteaClient(IHttpClientFactory clients, IOptions<GiteaOptions> options) : IGiteaClient
{
    public async Task<string> GetTextAsync(string token, string path, CancellationToken ct)
    {
        try {
        using var response = await SendAsync(token, path, "text/plain", ct);
        if (response.Content.Headers.ContentLength > 256000) throw new ApiException(413, "repository_diff_limit", "變更內容超過 256 KB，請縮小 commit 區間。");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var buffer = new MemoryStream(); var block = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(block, timeout.Token)) > 0) {
            if (buffer.Length + count > 256000) throw new ApiException(413, "repository_diff_limit", "變更內容超過 256 KB，請縮小 commit 區間。");
            buffer.Write(block, 0, count);
        }
        try { return new UTF8Encoding(false, true).GetString(buffer.ToArray()); }
        catch (DecoderFallbackException) { throw new ApiException(400, "repository_diff_not_text", "變更內容不是 UTF-8 文字，無法 review。"); }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ApiException(504, "gitea_timeout", "Gitea 讀取逾時。"); }
        catch (Exception ex) when (ex is HttpRequestException or IOException) { throw new ApiException(503, "gitea_unavailable", "Gitea 目前無法使用，請稍後重試。"); }
    }
    private async Task<HttpResponseMessage> SendAsync(string token, string path, string accept, CancellationToken ct)
    {
        if (!path.StartsWith("api/v1/", StringComparison.Ordinal)) throw new InvalidOperationException("Controlled Gitea path required.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(options.Value.BaseUrl.TrimEnd('/') + "/"), path));
        request.Headers.Authorization = new AuthenticationHeaderValue("token", token); request.Headers.Accept.ParseAdd(accept);
        try {
            var response = await clients.CreateClient(ControlledHttpClients.Tools).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.IsSuccessStatusCode) return response;
            var status = response.StatusCode; response.Dispose();
            throw new ApiException(status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? 403 : status == HttpStatusCode.NotFound ? 404 : 503,
                "gitea_read_failed", status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? "Gitea 授權不足或 token 已失效，請重新連線並確認唯讀權限。" : "Gitea 項目無法讀取，請確認路徑、版本與權限。");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ApiException(504, "gitea_timeout", "Gitea 讀取逾時。"); }
        catch (HttpRequestException) { throw new ApiException(503, "gitea_unavailable", "Gitea 目前無法使用，請稍後重試。"); }
    }
    public async Task<JsonDocument> GetAsync(string token, string path, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        try
        {
            using var response = await SendAsync(token, path, "application/json", timeout.Token);
            return await BoundedHttpJson.ReadAsync(response, Math.Max(1024 * 1024, options.Value.MaxFileBytes * 2), timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ApiException(504, "gitea_timeout", "Gitea 讀取逾時。"); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException) { throw new ApiException(503, "gitea_unavailable", "Gitea 目前無法使用，請稍後重試。"); }
    }
}
