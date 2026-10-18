using System.Text;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Diagnostics;

/// <summary>符合篩選條件的事件匯出為已遮罩的 CSV，有獨立的權限政策與速率限制；匯出前會先寫稽核。</summary>
internal sealed class ExportSystemLogs(DiagnosticQuery query)
{
    public static void Map(RouteGroupBuilder logs) => logs
        .MapGet("/export", ([AsParameters] DiagnosticFilter filter, ICurrentUser user, HttpContext http, ExportSystemLogs handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, filter, ct).ToHttpResultAsync(csv =>
            {
                http.Response.Headers.CacheControl = "no-store";
                return TypedResults.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", "ai-nexus-logs.csv");
            }))
        .RequireAuthorization(DiagnosticConfiguration.ExportPolicy).RequireRateLimiting(DiagnosticsModule.ExportRateLimit).WithName("ExportSystemLogs");

    public Task<Result<string>> HandleAsync(Guid actor, DiagnosticFilter filter, CancellationToken ct) => query.ExportAsync(actor, filter, ct);
}
