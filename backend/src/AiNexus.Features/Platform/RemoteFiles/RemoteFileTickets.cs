using System.Security.Cryptography;
using System.Text.Json;
using AiNexus.Platform.RemoteFiles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Platform.RemoteFiles;

/// <summary>
/// 給瀏覽器的遠端檔案：<paramref name="Ticket"/> 交給 <c>GET /api/v1/remote-files/content</c> 取內容，<paramref name="ContentType"/> 依副檔名判斷，
/// 預覽元件據此決定能不能內嵌顯示。
/// </summary>
public sealed record RemoteFileDto(string Ticket, string FileName, string ContentType, bool IsImage);

/// <summary>
/// 遠端檔案的短期憑證（ADR 0021）：加密並簽章的字串，裡面是來源、位置、檔名、限定的使用者與到期時間，前端看不到也改不了。
/// 權限只在發憑證時判斷一次：呼叫端（例如出勤的表單）確認看的人看得到這筆資料後才呼叫 <see cref="Issue"/>，下載端點只驗憑證本身。
/// </summary>
/// <remarks>
/// 到期時間寫在內容裡、以 <see cref="TimeProvider"/> 檢查，不用 <c>ITimeLimitedDataProtector</c>：後者吃系統時鐘，測不了過期。
/// 憑證只給發給的那位使用者用；換了帳號（含測試身分）就失效。
/// </remarks>
public sealed class RemoteFileTickets(IDataProtectionProvider protection, TimeProvider clock, IOptions<RemoteFilesOptions> options)
{
    /// <summary>檔名的上限：憑證放在查詢字串，太長會撞到 IIS 的網址上限。</summary>
    public const int MaxFileNameLength = 120;

    private static readonly FileExtensionContentTypeProvider Types = new();
    private static readonly HashSet<string> Images = ["image/png", "image/jpeg", "image/gif", "image/webp", "image/bmp"];
    private readonly IDataProtector protector = protection.CreateProtector("AiNexus.RemoteFiles.Ticket.v1");

    /// <summary>發給 <paramref name="viewer"/> 一張讀 <paramref name="file"/> 的憑證。</summary>
    public RemoteFileDto Issue(RemoteFileReference file, string fileName, Guid viewer)
    {
        var name = fileName.Length > MaxFileNameLength ? fileName[..MaxFileNameLength] : fileName;
        var expires = clock.GetUtcNow().AddMinutes(options.Value.TicketMinutes).ToUnixTimeSeconds();
        var ticket = protector.Protect(JsonSerializer.Serialize(new Payload(file.Source, file.Reference, name, viewer, expires)));
        var type = ContentTypeOf(name);
        return new(ticket, name, type, Images.Contains(type));
    }

    /// <summary>解開憑證；不是這位使用者的、過期或被竄改的都回 null。</summary>
    public (RemoteFileReference File, string FileName)? Open(string ticket, Guid viewer)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(protector.Unprotect(ticket));
            if (payload is null || payload.User != viewer || payload.Expires <= clock.GetUtcNow().ToUnixTimeSeconds()) return null;
            return (new(payload.Source, payload.Reference), payload.Name);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            return null;
        }
    }

    /// <summary>依副檔名的媒體類型；不採信來源系統回的 Content-Type（舊系統常一律回 octet-stream 或 HTML）。</summary>
    public static string ContentTypeOf(string fileName) => Types.TryGetContentType(fileName, out var type) ? type : "application/octet-stream";

    private sealed record Payload(string Source, string Reference, string Name, Guid User, long Expires);
}
