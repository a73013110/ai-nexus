using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Data.Tables;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Jobs.Queue;

/// <summary>
/// 所有人的背景工作，預設新到舊；排序、篩選與分頁走共用的 <see cref="TableRequest"/>（ADR 0026）。「背景作業」頁的佇列分頁用，
/// 權限跟著該頁（模組註冊時設定），不是使用者自己的背景任務頁。
/// </summary>
internal sealed class ListQueuedJobs(AppDbContext db) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("", ([AsParameters] TableRequest table, ListQueuedJobs handler, CancellationToken ct) => handler.HandleAsync(table, ct))
        .Audited(JobsAudit.QueueRead)
        .WithName("ListQueuedJobs");

    public Task<Result<TablePage<QueuedJobDto>>> HandleAsync(TableRequest table, CancellationToken ct)
        => db.BackgroundJobs.AsNoTracking().ToTablePageAsync(table, Columns, x => x.ToQueuedDtos(db), ct);

    /// <summary>欄位 ID 與 <see cref="QueuedJobDto"/> 的屬性同名。</summary>
    private static readonly TableColumns<BackgroundJob> Columns = new TableColumns<BackgroundJob>()
        .Range("createdAt", x => x.CreatedAt)
        .Range("updatedAt", x => x.UpdatedAt)
        .Options("status", x => x.Status)
        .Options("kind", x => x.Kind)
        .Text("label", x => x.Label)
        .Range("attempt", x => x.Attempt)
        .Key(x => x.Id)
        .DefaultSort("createdAt", descending: true);
}
