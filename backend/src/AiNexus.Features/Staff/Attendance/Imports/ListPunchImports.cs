using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Staff.Attendance.Imports;

/// <summary>
/// 匯入紀錄，新到舊，每頁 25、50（預設）或 100 筆；<c>before</c> 是上一頁最後一筆的 ID（游標），不回傳總數。
/// <c>from</c>（含）、<c>until</c>（不含）依開始匯入的時間；<c>fileId</c> 或 <c>fileName</c>（<c>yyyyMMdd.txt</c>，完全相同）限定單一檔案；
/// <c>status</c> 是 <see cref="PunchImportStatuses"/> 之一。
/// </summary>
internal sealed class ListPunchImports(AppDbContext db)
{
    public static readonly int[] PageSizes = [25, 50, 100];

    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("", (Guid? fileId, string? fileName, string? status, DateTimeOffset? from, DateTimeOffset? until, Guid? before, int? take, ListPunchImports handler, CancellationToken ct)
            => handler.HandleAsync(fileId, fileName, status, from, until, before, take, ct).ToHttpResultAsync())
        .WithName("ListAttendancePunchImports");

    public async Task<Result<IReadOnlyList<PunchImportDto>>> HandleAsync(Guid? fileId, string? fileName, string? status, DateTimeOffset? from, DateTimeOffset? until, Guid? before, int? take, CancellationToken ct)
    {
        if (status is not null and not (PunchImportStatuses.Succeeded or PunchImportStatuses.Failed) || from > until || take is { } size && !PageSizes.Contains(size) || fileName?.Length > PunchFile.FileNameMaxLength)
            return AttendanceErrors.ImportFilterInvalid;
        var imports = db.Set<PunchImport>().AsNoTracking();
        if (fileId is { } id) imports = imports.Where(x => x.PunchFileId == id);
        if (fileName?.Trim() is { Length: > 0 } name) imports = imports.Where(x => db.Set<PunchFile>().Any(f => f.Id == x.PunchFileId && f.FileName == name));
        if (status is not null) imports = imports.Where(x => x.Status == status);
        if (from is { } start) imports = imports.Where(x => x.StartedAt >= start);
        if (until is { } end) imports = imports.Where(x => x.StartedAt < end);
        if (before is { } cursor)
        {
            var previous = await db.Set<PunchImport>().AsNoTracking().Where(x => x.Id == cursor).Select(x => (DateTimeOffset?)x.StartedAt).SingleOrDefaultAsync(ct);
            if (previous is not { } at) return AttendanceErrors.ImportFilterInvalid;
            imports = imports.Where(x => x.StartedAt < at || (x.StartedAt == at && x.Id.CompareTo(cursor) < 0));
        }
        return Result<IReadOnlyList<PunchImportDto>>.Ok(await imports.OrderByDescending(x => x.StartedAt).ThenByDescending(x => x.Id).Take(take ?? 50).ToDtos(db).ToListAsync(ct));
    }
}
