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

    public string Name => SourceName;

    public async Task<RemoteFileRead> ReadAsync(string reference, CancellationToken ct)
    {
        if (!GdAttachments.IsPath(reference)) return RemoteFileRead.NotFound;
        var settings = options.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.FileTimeoutSeconds));
        try
        {
            var url = new Uri(new Uri(settings.WebBaseUrl), "servlets/fileservlet?openFile=" + Uri.EscapeDataString(reference));
            using var response = await clients.CreateClient(ControlledHttpClients.Tools).GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode == HttpStatusCode.NotFound) return RemoteFileRead.NotFound;
            if (!response.IsSuccessStatusCode)
            {
                LogUnavailable(logger, "status", (int)response.StatusCode);
                return RemoteFileRead.Unavailable;
            }
            if (response.Content.Headers.ContentType?.MediaType == "text/html") return RemoteFileRead.NotFound;
            if (response.Content.Headers.ContentLength > settings.MaxFileBytes) return RemoteFileRead.TooLarge;

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var block = new byte[81920];
            int count;
            while ((count = await stream.ReadAsync(block, timeout.Token)) > 0)
            {
                if (buffer.Length + count > settings.MaxFileBytes) return RemoteFileRead.TooLarge;
                buffer.Write(block, 0, count);
            }
            return buffer.Length == 0 ? RemoteFileRead.NotFound : new(RemoteFileStatus.Found, buffer.ToArray());
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            LogUnavailable(logger, "timeout", 0);
            return RemoteFileRead.Unavailable;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            LogUnavailable(logger, ex.GetType().Name, 0);
            return RemoteFileRead.Unavailable;
        }
    }

    // 網址、路徑與回應內容都不記：路徑就是公文系統裡的檔名，記下來等於把下載位置寫進日誌。
    [LoggerMessage(EventId = 6201, EventName = "gd.file_unavailable", Level = LogLevel.Warning, Message = "GD file server is unavailable ({Reason}, {StatusCode}).")]
    private static partial void LogUnavailable(ILogger logger, string reason, int statusCode);
}
