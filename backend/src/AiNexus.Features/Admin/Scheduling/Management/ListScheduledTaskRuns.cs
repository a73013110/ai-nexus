using AiNexus.Data.App;
using AiNexus.Data.App.Scheduling;
using AiNexus.Features.Admin.Scheduling.Engine;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Data.Tables;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Scheduling.Management;

/// <summary>
/// 一個任務的執行紀錄，預設新到舊。排序、篩選與分頁走共用的 <see cref="TableRequest"/>（ADR 0026）；
/// 「隱藏沒事可做」是 <c>status</c> 欄的篩選。紀錄含手動執行者的姓名，所以稽核查閱。
/// </summary>
internal sealed class ListScheduledTaskRuns(AppDbContext db, ScheduledTaskCatalog catalog) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/{key}/runs", (string key, [AsParameters] TableRequest table, ListScheduledTaskRuns handler, CancellationToken ct)
            => handler.HandleAsync(key, table, ct))
        .Audited(SchedulingAudit.RunRead)
        .WithName("ListScheduledTaskRuns");

    public async Task<Result<TablePage<ScheduledTaskRunDto>>> HandleAsync(string key, TableRequest table, CancellationToken ct)
    {
        if (catalog.Find(key) is null) return SchedulingErrors.TaskNotFound;
        return await db.ScheduledTaskRuns.AsNoTracking().Where(x => x.TaskKey == key).ToTablePageAsync(table, Columns, x => x.ToDtos(db), ct);
    }

    /// <summary>欄位 ID 與 <see cref="ScheduledTaskRunDto"/> 的屬性同名。</summary>
    private static readonly TableColumns<ScheduledTaskRun> Columns = new TableColumns<ScheduledTaskRun>()
        .Range("startedAt", x => x.StartedAt)
        .Options("status", x => x.Status)
        .Options("triggerKind", x => x.TriggerKind)
        .Text("host", x => x.Host)
        .Key(x => x.Id)
        .DefaultSort("startedAt", descending: true);
}
