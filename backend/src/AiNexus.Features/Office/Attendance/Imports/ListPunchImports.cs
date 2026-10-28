using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Data.Tables;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Imports;

/// <summary>
/// 匯入紀錄，預設新到舊；排序、欄位篩選與分頁走共用的 <see cref="TableRequest"/>（ADR 0026），每天一個檔，資料少時一次回傳全部。
/// <c>from</c>（含）、<c>until</c>（不含）依開始匯入的時間；<c>fileId</c> 或 <c>fileName</c>（<c>yyyyMMdd.txt</c>，完全相同）限定單一檔案；
/// <c>status</c> 是 <see cref="PunchImportStatuses"/> 之一。
/// </summary>
internal sealed class ListPunchImports(AppDbContext db)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("", (Guid? fileId, string? fileName, string? status, DateTimeOffset? from, DateTimeOffset? until, [AsParameters] TableRequest table, ListPunchImports handler, CancellationToken ct)
            => handler.HandleAsync(fileId, fileName, status, from, until, table, ct).ToHttpResultAsync())
        .Audited(AttendanceAudit.ImportRead)
        .WithName("ListAttendancePunchImports");

    public async Task<Result<TablePage<PunchImportDto>>> HandleAsync(Guid? fileId, string? fileName, string? status, DateTimeOffset? from, DateTimeOffset? until, TableRequest table, CancellationToken ct)
    {
        if (status is not null and not (PunchImportStatuses.Succeeded or PunchImportStatuses.Failed) || from > until || fileName?.Length > PunchFile.FileNameMaxLength)
            return AttendanceErrors.ImportFilterInvalid;
        var imports = db.Set<PunchImport>().AsNoTracking();
        if (fileId is { } id) imports = imports.Where(x => x.PunchFileId == id);
        if (fileName?.Trim() is { Length: > 0 } name) imports = imports.Where(x => db.Set<PunchFile>().Any(f => f.Id == x.PunchFileId && f.FileName == name));
        if (status is not null) imports = imports.Where(x => x.Status == status);
        if (from is { } start) imports = imports.Where(x => x.StartedAt >= start);
        if (until is { } end) imports = imports.Where(x => x.StartedAt < end);
        return await imports.ToTablePageAsync(table, Columns(), x => x.ToDtos(db), ct);
    }

    /// <summary>欄位 ID 與 <see cref="PunchImportDto"/> 的屬性同名，前端在本機排序時直接取同名欄位。</summary>
    private TableColumns<PunchImport> Columns() => new TableColumns<PunchImport>()
        .Range("startedAt", x => x.StartedAt)
        .Text("fileName", x => db.Set<PunchFile>().Where(f => f.Id == x.PunchFileId).Select(f => f.FileName).FirstOrDefault())
        .Range("punchCount", x => x.PunchCount)
        .Options("triggerKind", x => x.TriggerKind)
        .Options("status", x => x.Status)
        .Key(x => x.Id)
        .DefaultSort("startedAt", descending: true);
}
