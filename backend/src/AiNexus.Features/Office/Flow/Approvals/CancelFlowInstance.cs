using AiNexus.Data.App;
using AiNexus.Data.App.Flow;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Office.Flow.Approvals;

/// <summary>申請人在結束前取消自己的申請；審核人的未讀通知一併改為已讀。會稽核。</summary>
internal sealed class CancelFlowInstance(FlowEngine engine, AuditTrail audit) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/instances/{id:guid}/cancel", (Guid id, ICurrentUser user, CancelFlowInstance handler, CancellationToken ct) =>
            handler.HandleAsync(id, user.Id, ct))
        .Audited(FlowAudit.InstanceCancelled)
        .WithName("CancelFlowInstance");

    public Task<Result> HandleAsync(Guid id, Guid actor, CancellationToken ct)
        => engine.ChangeAsync(id, flow =>
        {
            // 不是申請人的一律回找不到：與詳情頁相同，不透露申請是否存在。
            if (flow.Instance.ApplicantId != actor) return Task.FromResult<Result>(FlowErrors.InstanceNotFound);
            audit.Describe(id, actor, new { kind = flow.Instance.Kind });
            return engine.CloseAsync(flow, FlowStatuses.Cancelled, actor, ct);
        }, ct);
}
