using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Export;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.Diagnostics;

/// <summary>符合篩選條件的事件匯出為已遮罩的 CSV，有獨立的權限政策與速率限制；匯出前會先寫稽核。</summary>
internal sealed class ExportSystemLogs(DiagnosticQuery query) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder logs) => logs
        .MapGet("/export", ([AsParameters] DiagnosticFilter filter, ICurrentUser user, ExportSystemLogs handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, filter, ct).ToFileResultAsync())
        .RequireFeature(FeatureCatalog.DiagnosticsLogsExport).RequireRateLimiting(DiagnosticsModule.ExportRateLimit).Audited(DiagnosticsAudit.LogExported).WithName("ExportSystemLogs");

    public Task<Result<ExportFile>> HandleAsync(Guid actor, DiagnosticFilter filter, CancellationToken ct) => query.ExportAsync(actor, filter, ct);
}
