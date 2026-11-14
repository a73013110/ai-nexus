using AiNexus.Data.App;
using AiNexus.Data.App.Flow;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Flow.Approvals;

/// <summary>
/// 申請清單，新的在前、以前一頁最後一筆的 ID 接續。<c>view</c>：<c>mine</c> 我送出的、<c>decided</c> 我審過的、
/// <c>all</c> 全部（需要 <c>flow.approvals.manage</c>）；<c>status</c> 篩選狀態。
/// </summary>
internal sealed class ListFlowInstances(AppDbContext db, FlowCatalog catalog, AccessService access) : IEndpointSlice
{
    public const string Mine = "mine", Decided = "decided", All = "all";
    private const int PageSize = 50;

    private static readonly string[] Statuses = [FlowStatuses.Pending, FlowStatuses.Approved, FlowStatuses.Rejected, FlowStatuses.Cancelled, FlowStatuses.Expired];

    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/instances", (string? view, string? status, Guid? before, ICurrentUser user, ListFlowInstances handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, view ?? Mine, status, before, ct))
        .Audited(FlowAudit.InstanceRead)
        .WithName("ListFlowInstances");

    public async Task<Result<FlowInstancePageDto>> HandleAsync(Guid actor, string view, string? status, Guid? before, CancellationToken ct)
    {
        if (status is not null && !Statuses.Contains(status)) return FlowErrors.ViewInvalid;
        IQueryable<FlowInstance> instances = db.FlowInstances.AsNoTracking();
        switch (view)
        {
            case Mine: instances = instances.Where(x => x.ApplicantId == actor); break;
            case Decided: instances = instances.Where(x => db.FlowInstanceSteps.Any(s => s.InstanceId == x.Id && s.DeciderId == actor)); break;
            case All when await access.HoldsAsync(actor, FeatureCatalog.FlowApprovalsManage, ct): break;
            case All: return AccessControlErrors.FeatureForbidden;
            default: return FlowErrors.ViewInvalid;
        }
        if (status is not null) instances = instances.Where(x => x.Status == status);
        if (before is { } cursor)
        {
            var previous = await instances.Where(x => x.Id == cursor).Select(x => (DateTimeOffset?)x.SubmittedAt).SingleOrDefaultAsync(ct);
            if (previous is not { } at) return FlowErrors.CursorInvalid;
            instances = instances.Where(x => x.SubmittedAt < at || (x.SubmittedAt == at && x.Id.CompareTo(cursor) < 0));
        }
        var rows = await FlowInstanceViews.Rows(db, instances).OrderByDescending(x => x.SubmittedAt).ThenByDescending(x => x.Id).Take(PageSize + 1).ToListAsync(ct);
        return new FlowInstancePageDto([.. rows.Take(PageSize).Select(x => x.Summary(catalog))], rows.Count > PageSize);
    }
}
