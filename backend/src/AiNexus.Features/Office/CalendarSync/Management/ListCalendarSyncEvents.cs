using AiNexus.Data.App;
using AiNexus.Data.App.CalendarSync;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Data.Tables;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.CalendarSync.Management;

/// <summary>
/// 日曆上的一段期間與它的同步狀態。<see cref="Removed"/> 表示它不該再出現在日曆（單被撤回、銷單或刪除），
/// 狀態仍是 pending／failed 時代表 Google 上的事件還沒刪掉。<see cref="HtmlLink"/> 是 Google 的事件網址，還沒建立過為 null。
/// </summary>
public sealed record CalendarSyncEventDto(
    Guid Id,
    string CalendarId,
    string DesNo,
    string Kind,
    string ApplicantName,
    string? AgentName,
    string FlowStatus,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string State,
    bool Removed,
    int Attempts,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? LastAttemptAt,
    string? LastErrorCode,
    string? LastError,
    DateTimeOffset? SyncedAt,
    string? HtmlLink,
    DateTimeOffset UpdatedAt);

/// <summary>
/// 日曆上的事件，預設依開始時間新到舊；<c>calendarId</c> 只看一個日曆，省略時看全部（管理頁預設帶本站設定的日曆）。
/// 預設只列同步還在管的事件（與比對同一個範圍，走同一個索引）；<c>history=true</c> 才含窗外的舊事件與已移除的列。
/// 排序、篩選與分頁走共用的 <see cref="TableRequest"/>（ADR 0026）。誰在什麼時候請假是個人資料，查詢寫稽核。
/// </summary>
internal sealed class ListCalendarSyncEvents(AppDbContext db, IOptions<CalendarSyncOptions> options, TimeProvider clock) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/events", ([AsParameters] TableRequest table, string? calendarId, bool? history, ListCalendarSyncEvents handler, CancellationToken ct)
            => handler.HandleAsync(table, calendarId, history == true, ct))
        .Audited(CalendarSyncAudit.EventRead)
        .WithName("ListCalendarSyncEvents");

    public Task<Result<TablePage<CalendarSyncEventDto>>> HandleAsync(TableRequest table, string? calendarId, bool history, CancellationToken ct)
    {
        var rows = db.CalendarEvents.AsNoTracking().OfCalendar(calendarId);
        if (!history) rows = rows.InSyncWindow(options.Value.WindowStart(clock.GetUtcNow()));
        return rows.ToTablePageAsync(table, Columns, page => page.Select(x => new CalendarSyncEventDto(
            x.Id, x.CalendarId, x.DesNo, x.Kind, x.ApplicantName, x.AgentName, x.FlowStatus, x.StartsAt, x.EndsAt, x.State, x.RemovedAt != null, x.Attempts, x.NextAttemptAt,
            x.LastAttemptAt, x.LastErrorCode, x.LastError, x.SyncedAt, x.HtmlLink, x.UpdatedAt)), ct);
    }

    /// <summary>欄位 ID 與 <see cref="CalendarSyncEventDto"/> 的屬性同名，前端在本機排序時直接取同名欄位。</summary>
    private static readonly TableColumns<CalendarEvent> Columns = new TableColumns<CalendarEvent>
    {
        // 一年數千列，超過門檻時改為逐頁讀取。
        CompleteUpTo = 1000
    }
        .Range("startsAt", x => x.StartsAt)
        .Text("calendarId", x => x.CalendarId)
        .Range("endsAt", x => x.EndsAt)
        .Text("desNo", x => x.DesNo)
        .Text("applicantName", x => x.ApplicantName)
        .Options("kind", x => x.Kind)
        .Options("flowStatus", x => x.FlowStatus)
        .Options("state", x => x.State)
        .Range("syncedAt", x => x.SyncedAt)
        .Range("updatedAt", x => x.UpdatedAt)
        .Key(x => x.Id)
        .DefaultSort("startsAt", descending: true);
}
