using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;

namespace AiNexus.Features.Office.Attendance.Imports;

/// <summary>
/// 一次匯入的紀錄。<see cref="FileName"/> 是來源檔名（<c>yyyyMMdd.txt</c>）；<see cref="RequestedById"/> 只有手動重匯才有值，
/// <see cref="RequestedByName"/> 取自使用者主檔。<see cref="PunchCount"/> 是這個檔匯入後擁有的刷卡筆數，扣掉已屬於別的檔的重複刷卡，失敗時為 0。
/// <see cref="ErrorMessage"/> 只在失敗時有值，是給 HR 看的說明（不含檔案內容與路徑）。
/// </summary>
public sealed record PunchImportDto(
    Guid Id,
    Guid PunchFileId,
    string FileName,
    string TriggerKind,
    Guid? RequestedById,
    string? RequestedByName,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    DateTimeOffset SourceModifiedAt,
    long SourceSize,
    int PunchCount,
    string Status,
    string? ErrorMessage);

internal static class PunchImportQueries
{
    /// <summary>匯入紀錄連同檔名與操作者姓名；排程的匯入沒有操作者，所以使用者是 left join。</summary>
    public static IQueryable<PunchImportDto> ToDtos(this IQueryable<PunchImport> imports, AppDbContext db)
        => from i in imports
           join f in db.Set<PunchFile>() on i.PunchFileId equals f.Id
           join u in db.Users on i.RequestedById equals u.Id into users
           from u in users.DefaultIfEmpty()
           select new PunchImportDto(i.Id, i.PunchFileId, f.FileName, i.TriggerKind, i.RequestedById, u.DisplayName, i.StartedAt, i.FinishedAt,
               i.SourceModifiedAt, i.SourceSize, i.PunchCount, i.Status, i.ErrorMessage);
}
