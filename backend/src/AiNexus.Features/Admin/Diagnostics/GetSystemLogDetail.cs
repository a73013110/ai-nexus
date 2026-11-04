using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.Diagnostics;

/// <summary>單一診斷事件的屬性與例外詳情，使用獨立的權限政策；讀取前會先寫稽核。</summary>
internal sealed class GetSystemLogDetail(DiagnosticQuery query) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder logs) => logs
        .MapGet("/{id:guid}", (Guid id, ICurrentUser user, GetSystemLogDetail handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct))
        .RequireFeature(FeatureCatalog.DiagnosticsLogsDetail).Audited(DiagnosticsAudit.LogDetailRead).WithName("GetSystemLogDetail");

    public Task<Result<DiagnosticDetail>> HandleAsync(Guid actor, Guid id, CancellationToken ct) => query.DetailAsync(actor, id, ct);
}
