using System.Net;
using AiNexus.Platform.Http;
using AiNexus.Platform.RemoteFiles;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Platform.Gd;

/// <summary>
/// 從公文系統網站取回附件（<c>servlets/fileservlet?openFile=&lt;路徑&gt;</c>）。那個網址沒有權限檢查，所以只在後端使用：
/// 瀏覽器拿的是 RemoteFiles 發的短期憑證，看不到公文系統的主機與路徑。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>路徑只接受 <see cref="GdAttachments.IsPath"/> 認可的格式，網址的主機與前綴固定取自 <see cref="GdOptions.WebBaseUrl"/>；受控客戶端不跟隨重新導向。</item>
/// <item>整個讀進記憶體再交給呼叫端（有 <see cref="GdOptions.MaxFileBytes"/> 上限），讀到一半斷線不會變成半個檔案。</item>
/// <item>舊式 servlet 找不到檔案時多半仍回 200，內容是空的或一頁 HTML；兩者都當成沒有這個檔案（附件本身不會是 HTML 頁）。</item>
/// </list>
/// </remarks>
public sealed partial class GdFileSource(IHttpClientFactory clients, IOptions<GdOptions> options, ILogger<GdFileSource> logger) : IRemoteFileSource
{
    public const string SourceName = "gd";

    public Task<RemoteFileRead> ReadAsync(string reference, CancellationToken ct)
    {
        if (!GdAttachments.IsPath(reference)) return Task.FromResult(RemoteFileRead.NotFound);
        return ExternalHttp.CallAsync(TimeSpan.FromSeconds(options.Value.FileTimeoutSeconds), timeout => DownloadAsync(reference, timeout), (failure, ex) =>
        {
            LogUnavailable(logger, failure == ExternalFailure.TimedOut ? "timeout" : ex.GetType().Name, 0);
            return RemoteFileRead.Unavailable;
        }, ct);
    }

    private async Task<RemoteFileRead> DownloadAsync(string reference, CancellationToken timeout)
    {
        var settings = options.Value;
        var url = new Uri(new Uri(settings.WebBaseUrl), "servlets/fileservlet?openFile=" + Uri.EscapeDataString(reference));
        using var response = await clients.CreateClient(ControlledHttpClients.Tools).GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout);
        if (response.StatusCode == HttpStatusCode.NotFound) return RemoteFileRead.NotFound;
        if (!response.IsSuccessStatusCode)
        {
            LogUnavailable(logger, "status", (int)response.StatusCode);
            return RemoteFileRead.Unavailable;
        }
        if (response.Content.Headers.ContentType?.MediaType == "text/html") return RemoteFileRead.NotFound;
        if (await BoundedHttpJson.TryReadBytesAsync(response, settings.MaxFileBytes, timeout) is not { } bytes) return RemoteFileRead.TooLarge;
        return bytes.Length == 0 ? RemoteFileRead.NotFound : new(RemoteFileStatus.Found, bytes);
    }

    // 網址、路徑與回應內容都不記：路徑就是公文系統裡的檔名，記下來等於把下載位置寫進日誌。
    [LoggerMessage(EventId = 6201, EventName = "gd.file_unavailable", Level = LogLevel.Warning, Message = "GD file server is unavailable ({Reason}, {StatusCode}).")]
    private static partial void LogUnavailable(ILogger logger, string reason, int statusCode);
}
