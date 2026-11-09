using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Strings;

namespace AiNexus.Features.Platform.Google;

/// <summary>
/// 所有 Google API 共用的回應處理：成功時以 source generation 的型別資訊反序列化，失敗時依狀態碼與錯誤原因分類成 <see cref="GoogleStatus"/>，
/// 例外（逾時、網路、換發權杖失敗）也轉成同一種結果，呼叫端不必各自寫 try/catch。新增其他 Google API 的客戶端時沿用這裡。
/// </summary>
internal static class GoogleHttp
{
    /// <summary>單一回應的上限；日曆事件清單一頁最多 2500 筆，遠小於此。</summary>
    private const int MaxResponseBytes = 8 * 1024 * 1024;

    /// <summary>沒有 <c>Retry-After</c> 時的退避：Google 建議從 1 秒起指數退避，這裡由呼叫端的重試排程再往上加。</summary>
    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(30);

    /// <summary>送出請求並讀取回應；<paramref name="type"/> 為 null 表示不讀內容（例如刪除）。逾時由 <paramref name="timeout"/> 控制，呼叫端取消照常拋出。</summary>
    public static async Task<GoogleResponse<T>> SendAsync<T>(HttpClient http, HttpRequestMessage request, JsonTypeInfo<T>? type, T emptyValue, TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            return await ExternalHttp.CallAsync(timeout, deadline => ExchangeAsync(http, request, type, emptyValue, deadline), Unavailable<T>, ct);
        }
        catch (GoogleAuthenticationException ex)
        {
            return new(ex.Status, Message: ex.Message);
        }
        catch (ExternalServiceException ex)
        {
            return Unavailable<T>(ExternalFailure.Unreachable, ex);
        }
    }

    private static async Task<GoogleResponse<T>> ExchangeAsync<T>(HttpClient http, HttpRequestMessage request, JsonTypeInfo<T>? type, T emptyValue, CancellationToken ct)
    {
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) return await FailureAsync<T>(response, ct);
        if (type is null || response.StatusCode == HttpStatusCode.NoContent) return GoogleResponse<T>.Ok(emptyValue);
        return JsonSerializer.Deserialize(await ReadBoundedAsync(response, ct), type) is { } value
            ? GoogleResponse<T>.Ok(value)
            : new(GoogleStatus.Unavailable, Message: "Google returned an empty response.");
    }

    private static GoogleResponse<T> Unavailable<T>(ExternalFailure failure, Exception ex)
        => new(GoogleStatus.Unavailable, Message: failure == ExternalFailure.TimedOut ? "Google did not respond in time." : ex.GetType().Name, RetryAfter: DefaultRetryAfter);

    private static async Task<GoogleResponse<T>> FailureAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var (message, reason) = await ErrorOfAsync(response, ct);
        var retryAfter = RetryAfterOf(response.Headers.RetryAfter);
        var status = (int)response.StatusCode switch
        {
            400 => GoogleStatus.Invalid,
            401 => GoogleStatus.CredentialRejected,
            403 when reason is "rateLimitExceeded" or "userRateLimitExceeded" or "quotaExceeded" or "dailyLimitExceeded" => GoogleStatus.RateLimited,
            403 => GoogleStatus.Forbidden,
            404 => GoogleStatus.NotFound,
            409 => GoogleStatus.Conflict,
            410 => GoogleStatus.Gone,
            429 => GoogleStatus.RateLimited,
            _ => GoogleStatus.Unavailable
        };
        var backoff = status is GoogleStatus.RateLimited or GoogleStatus.Unavailable ? retryAfter ?? DefaultRetryAfter : retryAfter;
        return new(status, Message: Truncate(message ?? $"HTTP {(int)response.StatusCode}"), RetryAfter: backoff);
    }

    /// <summary>Google 的錯誤格式是 <c>{"error":{"message":…,"errors":[{"reason":…}]}}</c>；權杖端點則是 <c>{"error":"invalid_grant","error_description":…}</c>。</summary>
    internal static async Task<(string? Message, string? Reason)> ErrorOfAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var document = JsonDocument.Parse(await ReadBoundedAsync(response, ct));
            if (!document.RootElement.TryGetProperty("error", out var error)) return (null, null);
            if (error.ValueKind == JsonValueKind.String)
                return (document.RootElement.TryGetProperty("error_description", out var description) ? description.GetString() : error.GetString(), error.GetString());
            var message = error.TryGetProperty("message", out var text) ? text.GetString() : null;
            var reason = error.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0
                && errors[0].TryGetProperty("reason", out var first) ? first.GetString() : null;
            return (message, reason);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    internal static Task<byte[]> ReadBoundedAsync(HttpResponseMessage response, CancellationToken ct)
        => BoundedHttpJson.ReadBytesAsync(response, MaxResponseBytes, ct);

    private static TimeSpan? RetryAfterOf(RetryConditionHeaderValue? header) => header switch
    {
        { Delta: { } delta } => delta,
        _ => null
    };

    internal static string Truncate(string text) => text.Clip(400);
}
