using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Export;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Records;

/// <summary>
/// 出勤紀錄匯出 Excel：條件、區間上限、資料範圍與判定都直接沿用 <see cref="ListAttendanceRecords"/>（同一次查詢、同一個驗證），
/// 只把結果排成工作表，看不到的人也匯不出來。檔案與舊系統 EmpClock 的「出缺勤計算」一模一樣（版面見 <see cref="AttendanceReportWorkbook"/>），
/// HR 核對、列印與轉交的習慣不變。
/// 匯出是把個人出勤資料帶離系統，稽核寫不進去就不匯出（與稽核、日誌匯出相同）。
/// </summary>
internal sealed class ExportAttendanceRecords(ListAttendanceRecords records, AppDbContext db, TimeProvider clock, AuditTrail audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/export", ([AsParameters] AttendanceRecordsFilter filter, ICurrentUser user, ExportAttendanceRecords handler, CancellationToken ct)
            => handler.HandleAsync(user.Id, filter, ct)
                .ToHttpResultAsync(file => TypedResults.File(file.Content, Spreadsheet.ContentType, file.Name)))
        .RequireRateLimiting(AttendanceModule.ExportRateLimit)
        .Audited(AttendanceAudit.RecordExported)
        .WithName("ExportAttendanceRecords");

    /// <summary>檔名與舊系統相同：「出缺勤計算_匯出時間(起to迄).xlsx」，匯出時間是台灣時間，起迄是查詢區間。</summary>
    internal sealed record ExportedFile(string Name, byte[] Content);

    public async Task<Result<ExportedFile>> HandleAsync(Guid viewer, AttendanceRecordsFilter filter, CancellationToken ct)
    {
        // 不分頁：匯出整份名單，區間上限與畫面相同（一年）。
        var read = await records.HandleAsync(viewer, filter, ListAttendanceRecords.Page.All, ct);
        if (!read.IsSuccess) return read.Error;
        var data = read.Value;
        // 稽核記查詢條件與人數，不記名單本身；請求稽核在回應送出前寫入，寫不進去檔案就不會送出（失敗即拒絕）。
        audit.Describe(details: new { count = data.People.Count });

        var exportedAt = TaipeiTime.Of(clock.GetUtcNow());
        var content = AttendanceReportWorkbook.Write(data, await CardsAsync(data, ct));
        return new ExportedFile(Invariant($"出缺勤計算_{exportedAt:yyyyMMdd_HHmmss}({data.From:yyyyMMdd}to{data.To:yyyyMMdd}).xlsx"), content);
    }

    /// <summary>
    /// 舊檔第二欄是卡號：使用者寫他在區間內持有的卡（換過卡時依開始持有的順序以「、」相連）；沒有帳號的資料本來就依附在卡號上，不必查。
    /// </summary>
    private async Task<Dictionary<Guid, string>> CardsAsync(AttendanceRecordsDto data, CancellationToken ct)
    {
        List<Guid> users = [.. data.People.Where(x => x.UserId is not null).Select(x => x.UserId!.Value)];
        if (users.Count == 0) return [];
        var (start, end) = AttendanceRoster.Bounds((data.From, data.To < data.Today ? data.To : data.Today));
        var rows = await db.Set<CardAssignment>().AsNoTracking()
            .Where(a => a.UserId != null && users.Contains(a.UserId.Value) && a.StartsAt < end && (a.EndsAt == null || a.EndsAt > start))
            .Join(db.Set<Card>(), a => a.CardId, c => c.Id, (a, c) => new { a.UserId, a.StartsAt, c.CardNumber })
            .ToListAsync(ct);
        return rows.GroupBy(x => x.UserId!.Value)
            .ToDictionary(g => g.Key, g => string.Join("、", g.OrderBy(x => x.StartsAt).Select(x => x.CardNumber).Distinct()));
    }

    /// <summary>日期格式裡的 <c>/</c> 是文化相依的分隔符號，一律以不變文化格式化。</summary>
    private static string Invariant(FormattableString text) => FormattableString.Invariant(text);
}
