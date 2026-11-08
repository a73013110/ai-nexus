using AiNexus.Data.App;
using AiNexus.Data.App.CalendarSync;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Data.Tables;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.CalendarSync.Management;

/// <summary>一筆執行紀錄；<see cref="RequestedByName"/> 是手動同步的操作者姓名，排程為 null。</summary>
public sealed record CalendarSyncRunDto(
    Guid Id,
    string CalendarId,
    string TriggerKind,
    Guid? RequestedById,
    string? RequestedByName,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string Status,
    int? SourceCount,
    int CreatedCount,
    int UpdatedCount,
    int DeletedCount,
    int FailedCount,
    int HeldCount,
    string? ErrorCode,
    string? ErrorMessage,
    int? LookbackDays);

/// <summary>
/// 執行紀錄，預設新到舊；<c>calendarId</c> 只看一個日曆，省略時看全部。排序、篩選與分頁走共用的 <see cref="TableRequest"/>（ADR 0026）。
/// </summary>
internal sealed class ListCalendarSyncRuns(AppDbContext db) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/runs", ([AsParameters] TableRequest table, string? calendarId, ListCalendarSyncRuns handler, CancellationToken ct)
            => handler.HandleAsync(table, calendarId, ct))
        .Audited(CalendarSyncAudit.RunRead)
        .WithName("ListCalendarSyncRuns");

    public Task<Result<TablePage<CalendarSyncRunDto>>> HandleAsync(TableRequest table, string? calendarId, CancellationToken ct)
        => db.SyncRuns.AsNoTracking().OfCalendar(calendarId).ToTablePageAsync(table, Columns, x => x.ToDtos(db), ct);

    /// <summary>欄位 ID 與 <see cref="CalendarSyncRunDto"/> 的屬性同名，前端在本機排序時直接取同名欄位。</summary>
    private static readonly TableColumns<SyncRun> Columns = new TableColumns<SyncRun>()
        .Range("startedAt", x => x.StartedAt)
        .Text("calendarId", x => x.CalendarId)
        .Options("triggerKind", x => x.TriggerKind)
        .Options("status", x => x.Status)
        .Range("sourceCount", x => x.SourceCount)
        .Range("createdCount", x => x.CreatedCount)
        .Range("updatedCount", x => x.UpdatedCount)
        .Range("deletedCount", x => x.DeletedCount)
        .Range("failedCount", x => x.FailedCount)
        .Key(x => x.Id)
        .DefaultSort("startedAt", descending: true);
}
