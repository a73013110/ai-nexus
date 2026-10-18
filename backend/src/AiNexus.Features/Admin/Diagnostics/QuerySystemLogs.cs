using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Diagnostics;

/// <summary>符合篩選條件的一頁診斷事件；驗證、稽核與分頁都由 <see cref="DiagnosticQuery"/> 處理。</summary>
internal sealed class QuerySystemLogs(DiagnosticQuery query)
{
    public static void Map(RouteGroupBuilder logs) => logs
        .MapGet("", ([AsParameters] DiagnosticFilter filter, ICurrentUser user, QuerySystemLogs handler, CancellationToken ct) => handler.HandleAsync(user.Id, filter, ct).ToHttpResultAsync())
        .WithName("QuerySystemLogs");

    public Task<Result<DiagnosticPage>> HandleAsync(Guid actor, DiagnosticFilter filter, CancellationToken ct) => query.ListAsync(actor, filter, ct);
}
