using System.Text.Json;
using AiNexus.Platform.Errors;

namespace AiNexus.Platform.Http;

public static class BoundedHttpJson
{
    public static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, int maxBytes, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength > maxBytes) throw new ApiException(502, "remote_response_too_large", "外部服務回應超過上限。");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream(); var bytes = new byte[8192];
        while (true)
        {
            var count = await stream.ReadAsync(bytes, ct); if (count == 0) break;
            if (buffer.Length + count > maxBytes) throw new ApiException(502, "remote_response_too_large", "外部服務回應超過上限。");
            buffer.Write(bytes, 0, count);
        }
        return JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
    }
}
