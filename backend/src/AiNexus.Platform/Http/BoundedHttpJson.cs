using System.Text.Json;
using AiNexus.Platform.Errors;

namespace AiNexus.Platform.Http;

/// <summary>提供受嚴格位元組上限防護的 HTTP JSON 回應讀取機制，防止惡意過大回應導致記憶體耗盡。</summary>
public static class BoundedHttpJson
{
    /// <summary>
    /// 在指定的位元組上限內讀取遠端回應串流並解析為 <see cref="JsonDocument"/>。
    /// 若 Content-Length 標頭或實際傳輸位元組數超過上限，立即拋出 <see cref="ExternalServiceException"/>。
    /// </summary>
    public static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, int maxBytes, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength > maxBytes) throw TooLarge();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var bytes = new byte[8192];

        while (true)
        {
            var count = await stream.ReadAsync(bytes, ct);
            if (count == 0) break;

            if (buffer.Length + count > maxBytes) throw TooLarge();
            buffer.Write(bytes, 0, count);
        }

        return JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
    }

    private static ExternalServiceException TooLarge() => new(Error.Upstream("remote_response_too_large"), "外部服務回應超過上限。");
}
