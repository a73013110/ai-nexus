using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.RemoteFiles;
using AiNexus.Platform.Security;

namespace AiNexus.Features.Platform.RemoteFiles;

/// <summary>
/// 以憑證取回遠端檔案並串流給瀏覽器（內嵌預覽或 <c>download=true</c> 下載），回應沿用 <see cref="WebSecurity.File(HttpContext, byte[], string, string, bool)"/> 的沙箱標頭。
/// 憑證放查詢字串而不是路徑：加密後約三、四百字，超過 IIS 單一路徑段 260 字的上限。
/// </summary>
internal sealed class DownloadRemoteFile(RemoteFileTickets tickets, IEnumerable<IRemoteFileSource> sources)
{
    /// <summary>超過這個長度一定不是本系統發的憑證，不必解密。</summary>
    public const int MaxTicketLength = 2000;

    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/content", (string ticket, bool? download, HttpContext http, ICurrentUser user, DownloadRemoteFile handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, ticket, ct).ToHttpResultAsync(x => WebSecurity.File(http, x.Content, x.ContentType, x.FileName, download == true)))
        .Audited(RemoteFilesAudit.FileDownloaded)
        .WithName("DownloadRemoteFile");

    public async Task<Result<(byte[] Content, string ContentType, string FileName)>> HandleAsync(Guid viewer, string ticket, CancellationToken ct)
    {
        if (ticket.Length > MaxTicketLength || tickets.Open(ticket, viewer) is not var (file, fileName)) return RemoteFilesErrors.NotFound;
        if (sources.FirstOrDefault(x => x.Name == file.Source) is not { } source) return RemoteFilesErrors.NotFound;
        var read = await source.ReadAsync(file.Reference, ct);
        return read.Status switch
        {
            RemoteFileStatus.Found => (read.Content!, RemoteFileTickets.ContentTypeOf(fileName), fileName),
            RemoteFileStatus.TooLarge => RemoteFilesErrors.TooLarge,
            RemoteFileStatus.Unavailable => RemoteFilesErrors.Unavailable,
            _ => RemoteFilesErrors.NotFound,
        };
    }
}
