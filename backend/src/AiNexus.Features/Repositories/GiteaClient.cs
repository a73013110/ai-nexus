using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Repositories;

/// <summary>Gitea REST API 的存取介面；每個呼叫都以使用者自己的 token 進行。</summary>
public interface IGiteaClient
{
    Task<JsonDocument> GetAsync(string token, string path, CancellationToken ct);
    /// <summary>UTF-8 文字內容；超過 <paramref name="maxBytes"/> 時回傳 null。</summary>
    Task<string?> GetTextAsync(string token, string path, int maxBytes, CancellationToken ct);
    /// <summary>唯一的寫入操作：在 issue 或 pull request 上留言。</summary>
    Task<JsonDocument> PostCommentAsync(string token, string path, string body, CancellationToken ct);
}

/// <summary>
/// 只能連到設定中的 Gitea 主機。遠端狀態碼會轉成固定的公開錯誤；遠端回應內容、token 與重新導向都不會傳給呼叫端，也不會寫進日誌。
/// </summary>
public sealed partial class GiteaClient(IHttpClientFactory clients, IOptions<GiteaOptions> options) : IGiteaClient
{
    public async Task<JsonDocument> GetAsync(string token, string path, CancellationToken ct)
    {
        using var timeout = Timeout(ct);
        try
        {
            using var response = await SendAsync(token, HttpMethod.Get, path, "application/json", null, timeout.Token);
            return await BoundedHttpJson.ReadAsync(response, Math.Max(1024 * 1024, options.Value.MaxFileBytes * 2), timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ExternalServiceException(RepositoriesErrors.Timeout); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException) { throw new ExternalServiceException(RepositoriesErrors.Unreachable); }
    }

    public async Task<string?> GetTextAsync(string token, string path, int maxBytes, CancellationToken ct)
    {
        using var timeout = Timeout(ct);
        try
        {
            using var response = await SendAsync(token, HttpMethod.Get, path, "text/plain", null, timeout.Token);
            if (response.Content.Headers.ContentLength > maxBytes) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var block = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(block, timeout.Token)) > 0)
            {
                if (buffer.Length + count > maxBytes) return null;
                buffer.Write(block, 0, count);
            }
            try { return new UTF8Encoding(false, true).GetString(buffer.ToArray()); }
            catch (DecoderFallbackException) { throw new ExternalServiceException(RepositoriesErrors.DiffNotText); }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ExternalServiceException(RepositoriesErrors.Timeout); }
        catch (Exception ex) when (ex is HttpRequestException or IOException) { throw new ExternalServiceException(RepositoriesErrors.Unreachable); }
    }

    public async Task<JsonDocument> PostCommentAsync(string token, string path, string body, CancellationToken ct)
    {
        if (!CommentPath().IsMatch(path)) throw new InvalidOperationException("Only issue comments may be written to Gitea.");
        using var timeout = Timeout(ct);
        try
        {
            using var response = await SendAsync(token, HttpMethod.Post, path, "application/json", JsonContent.Create(new { body }), timeout.Token);
            return await BoundedHttpJson.ReadAsync(response, 1024 * 1024, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ExternalServiceException(RepositoriesErrors.Timeout); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException) { throw new ExternalServiceException(RepositoriesErrors.Unreachable); }
    }

    private CancellationTokenSource Timeout(CancellationToken ct)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        return timeout;
    }

    private async Task<HttpResponseMessage> SendAsync(string token, HttpMethod method, string path, string accept, HttpContent? content, CancellationToken ct)
    {
        if (!path.StartsWith("api/v1/", StringComparison.Ordinal)) throw new InvalidOperationException("Controlled Gitea path required.");
        using var request = new HttpRequestMessage(method, new Uri(new Uri(options.Value.BaseUrl.TrimEnd('/') + "/"), path)) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("token", token);
        request.Headers.Accept.ParseAdd(accept);
        var response = await clients.CreateClient(ControlledHttpClients.Tools).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.IsSuccessStatusCode) return response;
        var status = response.StatusCode;
        response.Dispose();
        throw new ExternalServiceException(status switch
        {
            HttpStatusCode.Unauthorized => RepositoriesErrors.Unauthorized,
            HttpStatusCode.Forbidden => RepositoriesErrors.Forbidden,
            HttpStatusCode.NotFound => RepositoriesErrors.NotFound,
            _ => RepositoriesErrors.Unreachable,
        }, $"Gitea returned {(int)status}.");
    }

    [GeneratedRegex(@"^api/v1/repos/[^/?#]+/[^/?#]+/issues/[1-9][0-9]{0,8}/comments$")]
    private static partial Regex CommentPath();
}
