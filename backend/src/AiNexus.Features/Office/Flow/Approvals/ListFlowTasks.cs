using AiNexus.Data.App;
using AiNexus.Data.App.Flow;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Flow.Approvals;

/// <summary>
/// 待我審核：指派快取（<c>flow.FlowAssignments</c>）的索引查詢，舊的在前。指派由排程重算，組織剛異動時可能短暫落後；
/// 決定時會即時重算審核人，所以這裡多列或少列都不會讓錯的人核准。
/// </summary>
internal sealed class ListFlowTasks(AppDbContext db, FlowCatalog catalog) : IEndpointSlice
{
    /// <summary>同時待審超過這個數字代表流程設定有問題；畫面只顯示最舊的這些。</summary>
    public const int Limit = 200;

    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/tasks", (ICurrentUser user, ListFlowTasks handler, CancellationToken ct) => handler.HandleAsync(user.Id, ct))
        .NotAudited("使用者自己的待審清單；開啟申請時才稽核")
        .WithName("ListFlowTasks");

    public async Task<Result<IReadOnlyList<FlowInstanceSummaryDto>>> HandleAsync(Guid actor, CancellationToken ct)
    {
        var assigned = db.FlowInstances.AsNoTracking()
            .Where(x => x.Status == FlowStatuses.Pending && db.FlowAssignments.Any(a => a.InstanceId == x.Id && a.UserId == actor));
        var rows = await FlowInstanceViews.Rows(db, assigned).OrderBy(x => x.SubmittedAt).ThenBy(x => x.Id).Take(Limit).ToListAsync(ct);
        return rows.Select(x => x.Summary(catalog)).ToArray();
    }
}
