using System.Text.RegularExpressions;
using AiNexus.Platform.Data.External;
using AiNexus.Platform.RemoteFiles;

namespace AiNexus.Features.Platform.Gd;

/// <summary>
/// 公文系統的一個附件（<c>Frm_All_attFile</c>）。<paramref name="File"/> 交給 RemoteFiles 發憑證，由 <see cref="GdFileSource"/> 取回內容；
/// <paramref name="Uploader"/> 是 <c>Owner</c> 對到的姓名，對不到時為 null。
/// </summary>
public sealed record GdAttachment(string FileName, string? Memo, string? Uploader, RemoteFileReference File);

/// <summary>
/// 讀一張表單或公文的附件清單，表單、簽呈共用。GD 不可用或查詢被拒時回傳 null。
/// 只收位置合規則的列：附件路徑會拿去組公文系統的網址，<c>attFilePath</c> 必須是 <c>/attfile/&lt;民國年&gt;</c>、<c>attNo</c> 只有英數與 <c>._-</c>，
/// 其他的列（手動改過、舊版資料）略過，不讓資料庫裡的字串決定後端要連去哪裡。
/// </summary>
public sealed partial class GdAttachments(ExternalDatabase<GdDatabase> gd)
{
    private const string Sql = """
        SELECT a.[attNo] AS StoredName, a.[attFilePath] AS Folder, a.[attFileName] AS FileName, a.[attMemo] AS Memo, u.[NAME] AS Uploader
        FROM [dbo].[Frm_All_attFile] a
        LEFT JOIN [dbo].[Sys_User] u ON u.[ID] = a.[Owner]
        WHERE a.[DesNo] = @DesNo
        ORDER BY a.[attNo], a.[id]
        """;

    public async Task<IReadOnlyList<GdAttachment>?> ReadAsync(string desNo, CancellationToken ct)
    {
        var rows = await gd.QueryAsync<AttachmentRow>(Sql, new { DesNo = desNo }, ct);
        return rows?.Select(Attachment).OfType<GdAttachment>().ToList();
    }

    /// <summary>附件在公文系統網站上的路徑（<c>/attfile/115/L115100200101.jpg</c>）；位置不合規則時為 null。</summary>
    public static string? PathOf(string? folder, string? storedName)
    {
        var (path, name) = (folder?.Trim().TrimEnd('/'), storedName?.Trim());
        return path is not null && name is not null && Folder().IsMatch(path) && StoredName().IsMatch(name) && !name.Contains("..", StringComparison.Ordinal)
            ? path + "/" + name : null;
    }

    /// <summary><see cref="PathOf"/> 組出的路徑才是合法的附件位置；<see cref="GdFileSource"/> 取檔前再驗一次。</summary>
    public static bool IsPath(string reference)
    {
        var slash = reference.LastIndexOf('/');
        return slash > 0 && PathOf(reference[..slash], reference[(slash + 1)..]) == reference;
    }

    private static GdAttachment? Attachment(AttachmentRow row)
    {
        if (PathOf(row.Folder, row.StoredName) is not { } path) return null;
        var fileName = GdPeople.Text(row.FileName) ?? row.StoredName!.Trim();
        return new(fileName, GdPeople.Text(row.Memo), GdPeople.Text(row.Uploader), new(GdFileSource.SourceName, path));
    }

    [GeneratedRegex("^/attfile/[0-9]{2,3}$")]
    private static partial Regex Folder();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$")]
    private static partial Regex StoredName();

    private sealed class AttachmentRow
    {
        public string? StoredName { get; init; }
        public string? Folder { get; init; }
        public string? FileName { get; init; }
        public string? Memo { get; init; }
        public string? Uploader { get; init; }
    }
}
