using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using AiNexus.Features.Identity;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;

namespace AiNexus.Features.Diagnostics;

public sealed record ClientIssueRequest(string Kind, string Fingerprint);
public sealed record ClientIssueResponse(string IssueCode, bool Accepted);
public sealed class ClientIssueDeduplication : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 10000 });
    private readonly object gate = new();
    public ClientIssueResponse Record(Guid user, string fingerprint, Func<string, bool> admit)
    {
        lock (gate)
        {
            var key = user.ToString("N") + fingerprint;
            if (cache.TryGetValue<string>(key, out var existing)) return new(existing!, true);
            var code = Issues.NewCode();
            if (!admit(code)) return new(code, false);
            cache.Set(key, code, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) });
            return new(code, true);
        }
    }
    public void Dispose() => cache.Dispose();
}
public static class DiagnosticEndpoints
{
    public static void MapDiagnostics(this RouteGroupBuilder api)
    {
        var logs = api.MapGroup("/admin/logs").RequireAuthorization("feature:" + DiagnosticConfiguration.Query).WithTags("System logs").RequireRateLimiting(DiagnosticsModule.QueryRateLimit);
        logs.MapGet("", async ([AsParameters] DiagnosticFilter filter, DiagnosticQuery query, CancellationToken ct) => await query.ListAsync(filter, ct)).WithName("QuerySystemLogs").Produces<DiagnosticPage>();
        logs.MapGet("/health", async (DiagnosticQuery query, CancellationToken ct) => await query.HealthAsync(ct)).WithName("GetSystemLogHealth").Produces<DiagnosticHealthDto>();
        logs.MapGet("/{id:guid}", async (Guid id, DiagnosticQuery query, CancellationToken ct) => await query.DetailAsync(id, ct))
            .RequireAuthorization("feature:" + DiagnosticConfiguration.Detail).WithName("GetSystemLogDetail").Produces<DiagnosticDetail>();
        logs.MapGet("/export", async ([AsParameters] DiagnosticFilter filter, DiagnosticQuery query, HttpContext http, CancellationToken ct) => {
            http.Response.Headers.CacheControl = "no-store";
            return Results.File(Encoding.UTF8.GetBytes(await query.ExportAsync(filter, ct)), "text/csv; charset=utf-8", "ai-nexus-logs.csv");
        }).RequireAuthorization("feature:" + DiagnosticConfiguration.Export).RequireRateLimiting(DiagnosticsModule.ExportRateLimit).WithName("ExportSystemLogs");
        api.MapPost("/client-issues", async (ClientIssueRequest request, CurrentUser current, ClientIssueDeduplication dedup, ILogger<ClientIssueDeduplication> logger, CancellationToken ct) => {
            if (request.Kind is not ("exception" or "rejection") || request.Fingerprint is not { Length: 64 } || !request.Fingerprint.All(char.IsAsciiHexDigit))
                throw new ApiException(400, "client_issue_invalid", "問題回報格式不正確。");
            var user = (await current.GetAsync(ct)).Id;
            return dedup.Record(user, request.Kind + request.Fingerprint, code =>
            {
                var receipt = new DiagnosticDelivery();
                using var scope = logger.BeginScope(new Dictionary<string, object?> { ["NexusDelivery"] = receipt, ["IssueCode"] = code, ["UserId"] = user, ["UntrustedClient"] = true, ["ClientKind"] = request.Kind, ["ClientFingerprint"] = request.Fingerprint });
                logger.LogWarning(DiagnosticEvents.Client, "Untrusted client reported {ClientKind}; fingerprint {ClientFingerprint}.", request.Kind, request.Fingerprint);
                // Queue admission is not a synchronous durable-storage acknowledgement.
                return receipt.Accepted;
            });
        }).RequireRateLimiting(DiagnosticsModule.ClientIssueRateLimit).WithRequestBodyLimit(2048).WithName("ReportClientIssue").Produces<ClientIssueResponse>();
    }
}
