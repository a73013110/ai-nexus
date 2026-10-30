using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Office.Attendance.Punches;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Cards;

/// <summary>
/// 卡片詳情的刷卡紀錄，新到舊，每頁 <see cref="PageSize"/> 筆；<c>before</c> 是上一頁最後一筆的 ID（游標），不回傳總數。
/// 出勤紀錄以人為主、會判定遲到早退；這裡以卡片為主，只列原始刷卡與歸戶結果，讓 HR 查這張卡在誰手上時刷過哪些。
/// 刷卡依卡號字串對到卡片（與歸戶 view 同一個 join），所以卡片建立前就刷過的也會列出。
/// </summary>
internal sealed class ListCardPunches(AppDbContext db)
{
    public const int PageSize = 50;

    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/punches", (Guid id, long? before, ListCardPunches handler, CancellationToken ct) => handler.HandleAsync(id, before, ct).ToHttpResultAsync())
        .Audited(AttendanceAudit.PunchRead)
        .WithName("ListAttendanceCardPunches");

    public async Task<Result<IReadOnlyList<PunchDto>>> HandleAsync(Guid id, long? before, CancellationToken ct)
    {
        if (!await db.Set<Card>().AnyAsync(x => x.Id == id, ct)) return AttendanceErrors.CardNotFound;
        var punches = db.Set<AttributedPunch>().Where(x => x.CardId == id);
        if (before is { } cursor)
        {
            // 游標必須是這張卡的刷卡；換卡後沿用舊游標會跳到別張卡的時間點，直接拒絕。
            var previous = await punches.Where(x => x.PunchId == cursor).Select(x => (DateTimeOffset?)x.PunchedAt).FirstOrDefaultAsync(ct);
            if (previous is not { } at) return AttendanceErrors.FilterInvalid;
            punches = punches.Where(x => x.PunchedAt < at || (x.PunchedAt == at && x.PunchId < cursor));
        }
        var rows = await punches.OrderByDescending(x => x.PunchedAt).ThenByDescending(x => x.PunchId).Take(PageSize).ToDtos(db).ToListAsync(ct);
        return Result<IReadOnlyList<PunchDto>>.Ok(rows);
    }
}
