using System.Globalization;
using System.Text;
using System.Text.Json;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Audit;

/// <summary>
/// A CSV of every record matching the filters, newest first, under its own rate limit. A result over the configured row limit
/// is refused rather than cut short; the export is audited before it is returned.
/// </summary>
internal sealed class ExportActivityAudit(NexusDbContext db, IOptions<AuditOptions> options)
{
    public const string Action = "audit.export";
    private static readonly string[] Header = ["紀錄 ID", "時間（UTC）", "操作者", "測試身分", "動作", "資源 ID", "資源識別碼", "結果", "異動資訊", "分類", "Trace ID", "Operation ID", "查證代碼"];

    public static void Map(RouteGroupBuilder audit) => audit
        .MapGet("/export", ([AsParameters] ActivityAuditFilter filter, ICurrentUser user, HttpContext http, ExportActivityAudit handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, filter, ct).ToHttpResultAsync(csv =>
            {
                http.Response.Headers.CacheControl = "no-store";
                return TypedResults.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", "ai-nexus-audit.csv");
            }))
        .RequireRateLimiting(AuditModule.ExportRateLimit).WithName("ExportActivityAudit");

    public async Task<Result<string>> HandleAsync(Guid actor, ActivityAuditFilter filter, CancellationToken ct)
    {
        if (!filter.IsValid()) return AuditErrors.InvalidFilter;
        var limit = options.Value.MaxExportRows;
        var rows = await filter.Query(db).Take(limit + 1).ToListAsync(ct);
        if (rows.Count > limit) return AuditErrors.ExportLimit;
        // The export is a privileged read: it fails closed when its own record cannot be saved. Details keep to approved audit fields.
        db.AuditEvents.Add(new() { OwnerId = actor, Action = Action, Result = "csv", DetailsJson = JsonSerializer.Serialize(new { from = filter.From, to = filter.Until, traceId = filter.TraceId, count = rows.Count }) });
        await db.SaveChangesAsync(ct);
        var csv = new StringBuilder("\uFEFF").Append(Line(Header));
        foreach (var row in rows.Select(ActivityAuditFilter.Present))
            csv.Append(Line([row.Id.ToString(CultureInfo.InvariantCulture), row.At.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), row.Actor, row.ActingAs, row.Action,
                row.ResourceId?.ToString(), ResourceKey(row.DetailsJson), row.Result, row.DetailsJson, AuditCategories.Label(row.Category!), row.TraceId, row.OperationId?.ToString(), row.IssueCode]));
        return csv.ToString();
    }

    private static string Line(IEnumerable<string?> cells) => string.Join(",", cells.Select(CsvCell.Quote)) + "\r\n";

    /// <summary>The resource the record names in its details, as the audit page shows it.</summary>
    private static string? ResourceKey(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            using var details = JsonDocument.Parse(json);
            if (details.RootElement.ValueKind != JsonValueKind.Object) return null;
            foreach (var name in new[] { "resourceKey", "conversationId", "userId" })
                if (details.RootElement.TryGetProperty(name, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.False) && value.ToString() is { Length: > 0 } text)
                    return text;
        }
        catch (JsonException) { }
        return null;
    }
}
