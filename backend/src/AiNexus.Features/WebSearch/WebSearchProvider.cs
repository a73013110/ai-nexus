using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.WebSearch;

public interface IWebSearchProvider
{
    Task<IReadOnlyList<WebSourceDto>> SearchAsync(string query, CancellationToken ct);
}
public sealed partial class WebSearchProvider(IHttpClientFactory clients, IOptions<WebSearchOptions> options) : IWebSearchProvider
{
    public async Task<IReadOnlyList<WebSourceDto>> SearchAsync(string query, CancellationToken ct)
    {
        var o = options.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(o.TimeoutSeconds));
        var path = o.Provider == "brave" ? "https://api.search.brave.com/res/v1/web/search?q=" + Uri.EscapeDataString(query) + "&count=" + o.MaxResults
            : new Uri(new Uri(o.Endpoint.TrimEnd('/') + "/"), "search").AbsoluteUri + "?format=json&categories=general&safesearch=1&language=zh-TW&q=" + Uri.EscapeDataString(query);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.ParseAdd("application/json");
        if (o.Provider == "brave") request.Headers.Add("X-Subscription-Token", o.ApiKey);
        try
        {
            using var response = await clients.CreateClient("ControlledTools").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) throw new ApiException(response.StatusCode == HttpStatusCode.TooManyRequests ? 429 : 503, "web_search_unavailable", "網路搜尋服務無法使用，請確認服務設定或稍後重試。");
            using var json = await BoundedHttpJson.ReadAsync(response, 1024 * 1024, timeout.Token);
            JsonElement rows;
            if (o.Provider == "brave") { if (!json.RootElement.TryGetProperty("web", out var web) || !web.TryGetProperty("results", out rows)) return []; }
            else if (!json.RootElement.TryGetProperty("results", out rows)) return [];
            if (rows.ValueKind != JsonValueKind.Array) throw new JsonException();
            var hits = new List<WebSourceDto>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var time = DateTimeOffset.UtcNow;
            foreach (var row in rows.EnumerateArray().Take(50))
            {
                if (!row.TryGetProperty("url", out var url) || SafeUrl(url.GetString()) is not { } link || !seen.Add(link)) continue;
                var title = Clean(row.TryGetProperty("title", out var t) ? t.GetString() : null, 180);
                var snippet = Clean(row.TryGetProperty(o.Provider == "brave" ? "description" : "content", out var d) ? d.GetString() : null, 600);
                if (snippet.Length == 0) continue;
                hits.Add(new(hits.Count + 1, title.Length == 0 ? new Uri(link).Host : title, link, snippet, time));
                if (hits.Count >= o.MaxResults) break;
            }
            return hits;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ApiException(504, "web_search_timeout", "網路搜尋逾時，請稍後再試。"); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException) { throw new ApiException(503, "web_search_unavailable", "網路搜尋服務無法使用，請確認 JSON 搜尋介面已啟用。"); }
    }
    public static string? SafeUrl(string? value)
    {
        if (value?.Length > 2048 || !Uri.TryCreate(value, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https")
            || url.UserInfo.Length > 0 || url.IsLoopback || url.HostNameType == UriHostNameType.Unknown
            || !url.Host.Contains('.') || url.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)) return null;
        if (IPAddress.TryParse(url.Host, out var address))
        {
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            var bytes = address.GetAddressBytes();
            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 || bytes[0] is 0 or 10 or 127 or >= 224
                || (bytes[0] == 169 && bytes[1] == 254) || (bytes[0] == 192 && bytes[1] == 168) || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)) return null;
        }
        return url.AbsoluteUri;
    }
    private static string Clean(string? value, int max) => string.Concat(WebUtility.HtmlDecode(Tags().Replace(value ?? "", " ")).Where(c => !char.IsControl(c) || c == ' ').Take(max)).Trim();
    [GeneratedRegex("<[^>]*>", RegexOptions.CultureInvariant)] private static partial Regex Tags();
}
