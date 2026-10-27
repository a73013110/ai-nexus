using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Cards;

/// <summary>
/// 一筆刷卡從卡片的角度看：當時的持有人（<see cref="Holder"/> 是帳號顯示名稱，沒有帳號時是留下的姓名），
/// <see cref="Unattributed"/> 表示沒有持有期間涵蓋，不會出現在出勤紀錄。機器與事件代號原樣保存、不解讀。
/// </summary>
public sealed record CardPunchDto(long Id, DateTimeOffset PunchedAt, string MachineCode, string EventCode, bool Unattributed, Guid? UserId, string? Holder);

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

    public async Task<Result<IReadOnlyList<CardPunchDto>>> HandleAsync(Guid id, long? before, CancellationToken ct)
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
        var rows = await (
            from p in punches
            join u in db.Users on p.UserId equals u.Id into users
            from u in users.DefaultIfEmpty()
            orderby p.PunchedAt descending, p.PunchId descending
            select new CardPunchDto(p.PunchId, p.PunchedAt, p.MachineCode, p.EventCode, p.IsUnattributed, p.UserId, u != null ? u.DisplayName : p.HolderName))
            .Take(PageSize).ToListAsync(ct);
        return Result<IReadOnlyList<CardPunchDto>>.Ok(rows);
    }
}
