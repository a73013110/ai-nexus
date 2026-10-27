using AiNexus.Data.App;
using System.Globalization;
using System.Text;
using System.Text.Json;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Audit;

/// <summary>
/// 匯出符合條件的全部紀錄為 CSV（新到舊），有獨立的 rate limit。
/// 超過筆數上限時直接拒絕而不是截斷，避免使用者誤以為拿到完整資料。範圍與列表相同（<see cref="AuditDataScope"/>）。
/// </summary>
internal sealed class ExportActivityAudit(AppDbContext db, AuditDataScope scope, IOptions<AuditOptions> options)
{
    private static readonly string[] ResourceKeyFields = ["resourceKey", "conversationId", "userId"];
    private static readonly string[] Header = ["紀錄 ID", "時間（UTC）", "操作者", "測試身分", "動作", "分類", "功能", "資料主體", "資源 ID", "資源識別碼", "結果", "異動資訊", "來源 IP", "Trace ID", "Operation ID", "查證代碼"];

    public static void Map(RouteGroupBuilder audit) => audit
        .MapGet("/export", ([AsParameters] ActivityAuditFilter filter, ICurrentUser user, HttpContext http, ExportActivityAudit handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, filter, ct).ToHttpResultAsync(csv =>
            {
                http.Response.Headers.CacheControl = "no-store";
                return TypedResults.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", "ai-nexus-audit.csv");
            }))
        .RequireRateLimiting(AuditModule.ExportRateLimit).Audited(AuditAudit.EventExported).WithName("ExportActivityAudit");

    public async Task<Result<string>> HandleAsync(Guid actor, ActivityAuditFilter filter, CancellationToken ct)
    {
        if (!filter.IsValid()) return AuditErrors.InvalidFilter;
        // 匯出屬於特權讀取，與日誌匯出相同：請求稽核（含查詢條件）寫不進去就不送出（fail closed）；逾時或超過上限被拒的嘗試也會留下紀錄。
        db.Database.SetCommandTimeout(options.Value.ExportTimeoutSeconds);
        var limit = options.Value.MaxExportRows;
        // 多讀一筆只為判斷是否超過上限，不必另做 COUNT。
        var rows = await filter.Query(db, await scope.PeopleAsync(actor, FeatureCatalog.Audit, ct)).Take(limit + 1).ToListAsync(ct);
        if (rows.Count > limit) return AuditErrors.ExportLimit;
        var csv = new StringBuilder("\uFEFF").Append(Line(Header));
        foreach (var row in rows.Select(ActivityAuditFilter.Present))
            csv.Append(Line([row.Id.ToString(CultureInfo.InvariantCulture), row.At.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), row.Actor, row.ActingAs, row.Action,
                AuditCategories.Label(row.Category), row.Feature, row.Subject, row.ResourceId?.ToString(), ResourceKey(row.DetailsJson), row.Result, row.DetailsJson, row.ClientAddress,
                row.TraceId, row.OperationId?.ToString(), row.IssueCode]));
        return csv.ToString();
    }

    private static string Line(IEnumerable<string?> cells) => string.Join(",", cells.Select(CsvCell.Quote)) + "\r\n";

    /// <summary>詳情中記錄的資源識別碼，取法與稽核頁的「資源」欄相同。</summary>
    private static string? ResourceKey(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            using var details = JsonDocument.Parse(json);
            if (details.RootElement.ValueKind != JsonValueKind.Object) return null;
            foreach (var name in ResourceKeyFields)
                if (details.RootElement.TryGetProperty(name, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.False) && value.ToString() is { Length: > 0 } text)
                    return text;
        }
        catch (JsonException) { }
        return null;
    }
}
