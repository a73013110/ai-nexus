using AiNexus.Data.App;
using AiNexus.Data.App.CalendarSync;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Data.Tables;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.CalendarSync.Management;

/// <summary>一筆執行紀錄；<see cref="RequestedByName"/> 是手動同步的操作者姓名，排程為 null。</summary>
public sealed record CalendarSyncRunDto(
    Guid Id,
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
    string? ErrorMessage);

internal static class SyncRunQueries
{
    public static IQueryable<CalendarSyncRunDto> ToDtos(this IQueryable<SyncRun> runs, AppDbContext db)
        => from r in runs
           join u in db.Users on r.RequestedById equals u.Id into users
           from u in users.DefaultIfEmpty()
           select new CalendarSyncRunDto(r.Id, r.TriggerKind, r.RequestedById, u.DisplayName, r.StartedAt, r.FinishedAt, r.Status, r.SourceCount,
               r.CreatedCount, r.UpdatedCount, r.DeletedCount, r.FailedCount, r.HeldCount, r.ErrorCode, r.ErrorMessage);
}

/// <summary>目前設定的日曆的執行紀錄，預設新到舊；排序、篩選與分頁走共用的 <see cref="TableRequest"/>（ADR 0026）。</summary>
internal sealed class ListCalendarSyncRuns(AppDbContext db, IOptions<CalendarSyncOptions> options) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/runs", ([AsParameters] TableRequest table, ListCalendarSyncRuns handler, CancellationToken ct) => handler.HandleAsync(table, ct))
        .Audited(CalendarSyncAudit.RunRead)
        .WithName("ListCalendarSyncRuns");

    public Task<Result<TablePage<CalendarSyncRunDto>>> HandleAsync(TableRequest table, CancellationToken ct)
    {
        var calendarId = options.Value.CalendarId;
        return db.SyncRuns.AsNoTracking().Where(x => x.CalendarId == calendarId).ToTablePageAsync(table, Columns, x => x.ToDtos(db), ct);
    }

    /// <summary>欄位 ID 與 <see cref="CalendarSyncRunDto"/> 的屬性同名，前端在本機排序時直接取同名欄位。</summary>
    private static readonly TableColumns<SyncRun> Columns = new TableColumns<SyncRun>()
        .Range("startedAt", x => x.StartedAt)
        .Options("triggerKind", x => x.TriggerKind)
        .Options("status", x => x.Status)
        .Range("createdCount", x => x.CreatedCount)
        .Range("updatedCount", x => x.UpdatedCount)
        .Range("deletedCount", x => x.DeletedCount)
        .Range("failedCount", x => x.FailedCount)
        .Key(x => x.Id)
        .DefaultSort("startedAt", descending: true);
}
