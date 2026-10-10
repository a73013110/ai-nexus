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
        if (response.Content.Headers.ContentLength > 256000) throw new ExternalServiceException(RepositoriesErrors.DiffLimit);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var buffer = new MemoryStream(); var block = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(block, timeout.Token)) > 0) {
            if (buffer.Length + count > 256000) throw new ExternalServiceException(RepositoriesErrors.DiffLimit);
            buffer.Write(block, 0, count);
        }
        try { return new UTF8Encoding(false, true).GetString(buffer.ToArray()); }
        catch (DecoderFallbackException) { throw new ExternalServiceException(RepositoriesErrors.DiffNotText); }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ExternalServiceException(RepositoriesErrors.Timeout); }
        catch (Exception ex) when (ex is HttpRequestException or IOException) { throw new ExternalServiceException(RepositoriesErrors.Unreachable); }
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
            var kind = status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? ErrorKind.Forbidden : status == HttpStatusCode.NotFound ? ErrorKind.NotFound : ErrorKind.Unavailable;
            throw new ExternalServiceException(new(kind, "gitea_read_failed"), $"Gitea returned {(int)status}.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ExternalServiceException(RepositoriesErrors.Timeout); }
        catch (HttpRequestException) { throw new ExternalServiceException(RepositoriesErrors.Unreachable); }
    }
    public async Task<JsonDocument> GetAsync(string token, string path, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        try
        {
            using var response = await SendAsync(token, path, "application/json", timeout.Token);
            return await BoundedHttpJson.ReadAsync(response, Math.Max(1024 * 1024, options.Value.MaxFileBytes * 2), timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ExternalServiceException(RepositoriesErrors.Timeout); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException) { throw new ExternalServiceException(RepositoriesErrors.Unreachable); }
    }
}
