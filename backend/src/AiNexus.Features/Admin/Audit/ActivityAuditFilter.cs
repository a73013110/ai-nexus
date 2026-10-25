using AiNexus.Data.App;
using AiNexus.Platform.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Audit;

/// <summary>列表與匯出共用的稽核篩選條件；分頁只屬於列表。</summary>
public sealed class ActivityAuditFilter
{
    /// <summary>比對帳號、顯示名稱、動作、TraceId、查證代碼或詳情文字；輸入 GUID 時改比對資源 ID 或操作者／對象的使用者 ID。</summary>
    public string? Search { get; set; }
    /// <summary>完整動作或動作前綴（例如 <c>admin.</c>）。</summary>
    public string? Action { get; set; }
    /// <summary>符合任一結果即可。<c>success</c> 代表所有受理類結果、<c>failed</c> 代表其餘全部，其他值比對實際儲存的結果。</summary>
    public string[]? Result { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? Until { get; set; }
    public string? Category { get; set; }
    public string? TraceId { get; set; }

    internal bool IsValid()
    {
        var textInvalid = Search?.Length > 120 || Action?.Length > 120 || Result?.Length > 10 || Result?.Any(x => x is null || x.Length > 80) == true;
        var traceInvalid = TraceId is not null && (TraceId.Length != 32 || !TraceId.All(char.IsAsciiHexDigit));
        var categoryInvalid = !string.IsNullOrEmpty(Category) && !AuditCategories.Values.Contains(Category);
        return !(textInvalid || From > Until || traceInvalid || categoryInvalid);
    }

    /// <summary>
    /// 符合條件的紀錄，新到舊；有 <paramref name="before"/> 時只取 ID 比它小的（游標分頁）。
    /// <paramref name="people"/> 是 <see cref="AuditDataScope"/> 的範圍（null＝全部）。每列回傳前都要經過 <see cref="Present"/>。
    /// </summary>
    internal IQueryable<AuditDto> Query(AppDbContext db, IQueryable<Guid>? people, long? before = null)
    {
        // actor 是實際操作者（沒有記錄時視為擁有者本人），subject 是紀錄的擁有者。兩者不同代表代為操作，
        // 介面會以測試身分顯示，對象帳號則放進 ActingAs。
        var query = AuditCategories.Filter(db.AuditEvents.AsNoTracking(), Category)
            .LeftJoin(db.Users, entry => entry.ActorId ?? entry.OwnerId, user => user.Id, (entry, actor) => new { entry, actor })
            .LeftJoin(db.Users, x => x.entry.OwnerId, user => user.Id, (x, subject) => new { x.entry, x.actor, subject });
        if (people is not null) query = query.Where(x => people.Contains(x.entry.OwnerId) || x.entry.ActorId != null && people.Contains(x.entry.ActorId.Value));
        if (before is { } cursor) query = query.Where(x => x.entry.Id < cursor);
        if (From is { } start) query = query.Where(x => x.entry.At >= start);
        if (Until is { } end) query = query.Where(x => x.entry.At < end);
        if (TraceId?.ToLowerInvariant() is { } trace) query = query.Where(x => x.entry.TraceId == trace);
        if (!string.IsNullOrWhiteSpace(Action)) { var action = Action; query = query.Where(x => x.entry.Action.StartsWith(action)); }
        if (Result?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray() is { Length: > 0 } results)
        {
            bool success = results.Contains("success"), failed = results.Contains("failed");
            var exact = results.Where(x => x is not ("success" or "failed")).ToArray();
            query = query.Where(x => x.entry.Result != null && (success && AuditOutcomes.Accepted.Contains(x.entry.Result)
                || failed && !AuditOutcomes.Accepted.Contains(x.entry.Result) || exact.Contains(x.entry.Result)));
        }
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var search = Search;
            if (Guid.TryParse(search, out var resource)) query = query.Where(x => x.entry.ResourceId == resource || (x.actor != null && x.actor.Id == resource) || (x.subject != null && x.subject.Id == resource));
            else query = query.Where(x => (x.actor != null && ((x.actor.AdAccount ?? x.actor.LocalAccount ?? "").Contains(search) || x.actor.DisplayName.Contains(search))) || (x.subject != null && (x.subject.AdAccount ?? x.subject.LocalAccount ?? "").Contains(search)) || x.entry.Action.Contains(search) || x.entry.TraceId == search || x.entry.IssueCode == search || (x.entry.DetailsJson != null && x.entry.DetailsJson.Contains(search)));
        }
        // 依 ID 排序而非時間：ID 唯一且遞增，游標不會因同一時間的多筆紀錄而重複或漏掉。
        // 沒有對應使用者時，登入失敗顯示「未驗證」（帳號尚未驗證），其他視為系統動作。
        return query.OrderByDescending(x => x.entry.Id)
            .Select(x => new AuditDto(x.entry.Id, x.actor == null ? x.entry.Action == "identity.login" && x.entry.Result == "failed" ? "未驗證" : "system" : x.actor.AdAccount ?? x.actor.LocalAccount ?? "", x.entry.Action, x.entry.ResourceId, x.entry.Result, x.entry.At, x.entry.DetailsJson, x.entry.ActorId != null && x.entry.ActorId != x.entry.OwnerId && x.subject != null ? x.subject.AdAccount ?? x.subject.LocalAccount : null,
                null, x.entry.TraceId, x.entry.OperationId, x.entry.IssueCode));
    }

    /// <summary>補上分類，並在輸出時再遮罩一次詳情：舊紀錄可能是在遮罩規則加入前寫入的。</summary>
    internal static AuditDto Present(AuditDto row) => row with { Category = AuditCategories.For(row.Action), DetailsJson = AuditRedactor.Sanitize(row.DetailsJson, historical: true) };
}
