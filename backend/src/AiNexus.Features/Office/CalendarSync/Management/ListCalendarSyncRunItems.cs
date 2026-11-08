using AiNexus.Data.App;
using AiNexus.Data.App.CalendarSync;
using AiNexus.Features.Office.CalendarSync.Sync;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Data.Tables;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.CalendarSync.Management;

/// <summary>一輪逐筆處理的事件（當下的快照）；<see cref="Outcome"/> 與該輪的數量一一對應，另有被安全閥擋下的 <c>held</c>。</summary>
public sealed record CalendarSyncRunItemDto(
    Guid EventId,
    string Outcome,
    string DesNo,
    string Kind,
    string ApplicantName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string? Cause,
    IReadOnlyList<CalendarEventFieldChange> Changes,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>
/// 一輪同步的逐筆明細，預設依文號；排序、篩選與分頁走共用的 <see cref="TableRequest"/>（ADR 0026）。
/// 主鍵以 <c>RunId</c> 開頭，一輪的明細是一段連續的範圍，不需要另外的索引。誰在什麼時候請假是個人資料，查詢寫稽核。
/// </summary>
internal sealed class ListCalendarSyncRunItems(AppDbContext db) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/runs/{runId:guid}/items", (Guid runId, [AsParameters] TableRequest table, ListCalendarSyncRunItems handler, CancellationToken ct)
            => handler.HandleAsync(runId, table, ct))
        .Audited(CalendarSyncAudit.RunItemRead)
        .WithName("ListCalendarSyncRunItems");

    public async Task<Result<TablePage<CalendarSyncRunItemDto>>> HandleAsync(Guid runId, TableRequest table, CancellationToken ct)
    {
        if (!await db.SyncRuns.AnyAsync(x => x.Id == runId, ct)) return CalendarSyncErrors.RunNotFound;
        return await db.SyncRunItems.AsNoTracking().Where(x => x.RunId == runId).ToTablePageAsync(table, Columns, rows => rows.Select(x => new CalendarSyncRunItemDto(
            x.EventId, x.Outcome, x.DesNo, x.Kind, x.ApplicantName, x.StartsAt, x.EndsAt, x.Cause, CalendarEventChanges.Parse(x.ChangesJson), x.ErrorCode, x.ErrorMessage)), ct);
    }

    /// <summary>欄位 ID 與 <see cref="CalendarSyncRunItemDto"/> 的屬性同名，前端在本機排序時直接取同名欄位。</summary>
    private static readonly TableColumns<SyncRunItem> Columns = new TableColumns<SyncRunItem>()
        .Options("outcome", x => x.Outcome)
        .Text("applicantName", x => x.ApplicantName)
        .Options("kind", x => x.Kind)
        .Range("startsAt", x => x.StartsAt)
        .Text("desNo", x => x.DesNo)
        .Key(x => x.EventId)
        .DefaultSort("desNo");
}
