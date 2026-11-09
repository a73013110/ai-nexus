using System.Text.Json;
using AiNexus.Platform.Errors;

namespace AiNexus.Platform.Http;

/// <summary>讀取遠端 JSON 回應時設定位元組上限，避免惡意的超大回應耗盡記憶體。</summary>
public static class BoundedHttpJson
{
    private static readonly Error RemoteResponseTooLarge = Error.Upstream("remote_response_too_large");

    /// <summary>
    /// 在指定的位元組上限內讀取遠端回應串流並解析為 <see cref="JsonDocument"/>。
    /// 若 Content-Length 標頭或實際傳輸位元組數超過上限，立即拋出 <see cref="ExternalServiceException"/>。
    /// </summary>
    public static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, int maxBytes, CancellationToken ct)
        => JsonDocument.Parse(await ReadBytesAsync(response, maxBytes, ct), new JsonDocumentOptions { MaxDepth = 32 });

    /// <summary>
    /// 在位元組上限內讀完回應內容，交給呼叫端自己解析（例：source-generated 的 <c>JsonTypeInfo</c>）。
    /// 超過上限（標頭宣告或實際傳輸）時拋出 <see cref="ExternalServiceException"/>。
    /// </summary>
    public static async Task<byte[]> ReadBytesAsync(HttpResponseMessage response, int maxBytes, CancellationToken ct)
        => await TryReadBytesAsync(response, maxBytes, ct) ?? throw TooLarge();

    /// <summary>同 <see cref="ReadBytesAsync"/>，但超過上限回傳 null：太大是預期結果（例：略過過大的檔案）時用。</summary>
    public static async Task<byte[]?> TryReadBytesAsync(HttpResponseMessage response, int maxBytes, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength > maxBytes) return null;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var bytes = new byte[8192];

        while (true)
        {
            var count = await stream.ReadAsync(bytes, ct);
            if (count == 0) break;

            if (buffer.Length + count > maxBytes) return null;
            buffer.Write(bytes, 0, count);
        }

        return buffer.ToArray();
    }

    private static ExternalServiceException TooLarge() => new(RemoteResponseTooLarge, "外部服務回應超過上限。");
}
