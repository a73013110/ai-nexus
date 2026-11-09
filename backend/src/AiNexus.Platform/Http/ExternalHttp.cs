using System.Net;
using System.Text.Json;

namespace AiNexus.Platform.Http;

/// <summary>外部呼叫失敗的原因：等到期限，或傳輸、格式出錯。</summary>
public enum ExternalFailure
{
    TimedOut,
    Unreachable,
}

/// <summary>重送前的通知；<see cref="Status"/> 與 <see cref="Exception"/> 只會有一個有值。</summary>
public readonly record struct ExternalRetry(int Attempt, int? Status, Exception? Exception);

/// <summary>對外 HTTP 呼叫共用的期限、失敗分類與重送；回應上限見 <see cref="BoundedHttpJson"/>。串流推論不走這裡：它的期限看的是兩個片段之間的間隔。</summary>
public static class ExternalHttp
{
    /// <summary>
    /// 在 <paramref name="timeout"/> 內執行 <paramref name="call"/>，把期限與傳輸、格式錯誤交給 <paramref name="failed"/> 換成呼叫端的結果或例外。
    /// 呼叫端自己取消時照常拋出，不算失敗；原始例外的訊息可能含遠端內容，換成對外錯誤時不要帶上。
    /// </summary>
    public static async Task<T> CallAsync<T>(
        TimeSpan timeout,
        Func<CancellationToken, Task<T>> call,
        Func<ExternalFailure, Exception, T> failed,
        CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        try
        {
            return await call(deadline.Token);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            return failed(ExternalFailure.TimedOut, ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException)
        {
            return failed(ExternalFailure.Unreachable, ex);
        }
    }

    /// <summary>
    /// 送出請求，暫時性失敗最多再送 <paramref name="retries"/> 次，只回應標頭就返回。
    /// 只給重送沒有副作用的請求用；用完次數後最後一個回應照樣交給呼叫端，由它對應狀態碼。
    /// 每次都由 <paramref name="request"/> 建立新請求：<see cref="HttpRequestMessage"/> 送過就不能再用。
    /// </summary>
    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient http,
        Func<HttpRequestMessage> request,
        int retries,
        CancellationToken ct,
        Action<ExternalRetry>? retrying = null)
    {
        for (var attempt = 0; ; attempt++)
        {
            ExternalRetry retry;
            try
            {
                using var message = request();
                var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
                if (attempt >= retries || !IsTransient(response.StatusCode)) return response;
                retry = new(attempt + 1, (int)response.StatusCode, null);
                response.Dispose();
            }
            catch (HttpRequestException ex) when (attempt < retries)
            {
                retry = new(attempt + 1, null, ex);
            }
            retrying?.Invoke(retry);
            await Task.Delay(TimeSpan.FromMilliseconds(250 * (1 << attempt) + Random.Shared.Next(100)), ct);
        }
    }

    private static bool IsTransient(HttpStatusCode status)
        => status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500;
}
