using AiNexus.Platform.Modules;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Repositories;

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
[Scoped<IGiteaClient>]
public sealed partial class GiteaClient(IHttpClientFactory clients, IOptions<GiteaOptions> options) : IGiteaClient
{
    public Task<JsonDocument> GetAsync(string token, string path, CancellationToken ct) => CallAsync(async timeout =>
    {
        using var response = await SendAsync(token, HttpMethod.Get, path, "application/json", null, timeout);
        return await BoundedHttpJson.ReadAsync(response, Math.Max(1024 * 1024, options.Value.MaxFileBytes * 2), timeout);
    }, ct);

    public Task<string?> GetTextAsync(string token, string path, int maxBytes, CancellationToken ct) => CallAsync(async timeout =>
    {
        using var response = await SendAsync(token, HttpMethod.Get, path, "text/plain", null, timeout);
        if (await BoundedHttpJson.TryReadBytesAsync(response, maxBytes, timeout) is not { } bytes) return null;
        try { return new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { throw new ExternalServiceException(RepositoriesErrors.DiffNotText); }
    }, ct);

    public async Task<JsonDocument> PostCommentAsync(string token, string path, string body, CancellationToken ct)
    {
        if (!CommentPath().IsMatch(path)) throw new InvalidOperationException("Only issue comments may be written to Gitea.");
        return await CallAsync(async timeout =>
        {
            using var response = await SendAsync(token, HttpMethod.Post, path, "application/json", JsonContent.Create(new { body }), timeout);
            return await BoundedHttpJson.ReadAsync(response, 1024 * 1024, timeout);
        }, ct);
    }

    private Task<T> CallAsync<T>(Func<CancellationToken, Task<T>> call, CancellationToken ct) => ExternalHttp.CallAsync(
        TimeSpan.FromSeconds(options.Value.TimeoutSeconds),
        call,
        (failure, _) => throw new ExternalServiceException(failure == ExternalFailure.TimedOut ? RepositoriesErrors.Timeout : RepositoriesErrors.Unreachable),
        ct);

    private async Task<HttpResponseMessage> SendAsync(string token, HttpMethod method, string path, string accept, HttpContent? content, CancellationToken ct)
    {
        if (!path.StartsWith("api/v1/", StringComparison.Ordinal)) throw new InvalidOperationException("Controlled Gitea path required.");
        var response = await ExternalHttp.SendAsync(clients.CreateClient(ControlledHttpClients.Tools), () =>
        {
            var request = new HttpRequestMessage(method, new Uri(new Uri(options.Value.BaseUrl.TrimEnd('/') + "/"), path)) { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("token", token);
            request.Headers.Accept.ParseAdd(accept);
            return request;
        }, retries: method == HttpMethod.Get ? 2 : 0, ct); // 只有讀取能重送：留言重送會貼兩次。
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
