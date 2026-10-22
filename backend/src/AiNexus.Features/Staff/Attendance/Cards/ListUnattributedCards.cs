using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Staff.Attendance.Cards;

/// <summary>
/// 未歸戶卡號：員工卡上沒有持有期間涵蓋的刷卡，依卡號彙總。<see cref="FirstPunchedAt"/> 是指派時起始時間的預設值
/// （從第一筆未歸戶刷卡開始持有，之後的刷卡才會全部歸戶）；<see cref="CardId"/> 為 null 表示這個卡號還沒有登錄，要先登錄才能指派。
/// </summary>
public sealed record UnattributedCardDto(string CardNumber, Guid? CardId, int PunchCount, DateTimeOffset FirstPunchedAt, DateTimeOffset LastPunchedAt);

/// <summary>
/// 讀 <c>attendance.vw_AttributedPunches</c> 的未歸戶刷卡並在資料庫彙總。訪客卡不歸戶也不算未歸戶，view 已排除；
/// 持有人只有姓名（還沒有帳號）的刷卡算已歸戶。<c>from</c>、<c>to</c> 是出勤日（含），依刷卡時間而非檔名；最近有刷卡的卡號排最前面。
/// </summary>
internal sealed class ListUnattributedCards(AppDbContext db)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("", (DateOnly? from, DateOnly? to, ListUnattributedCards handler, CancellationToken ct) => handler.HandleAsync(from, to, ct).ToHttpResultAsync())
        .WithName("ListUnattributedAttendanceCards");

    public async Task<Result<IReadOnlyList<UnattributedCardDto>>> HandleAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (from > to) return AttendanceErrors.PeriodInvalid;
        var punches = db.Set<AttributedPunch>().Where(x => x.IsUnattributed);
        if (from is { } min) punches = punches.Where(x => x.AttendanceDate >= min);
        if (to is { } max) punches = punches.Where(x => x.AttendanceDate <= max);
        // view 已帶出登錄的卡（卡號唯一），CardId 跟著卡號分組即可，不必再查一次 Cards。
        return Result<IReadOnlyList<UnattributedCardDto>>.Ok(await punches
            .GroupBy(x => new { x.CardNumber, x.CardId })
            .OrderByDescending(g => g.Max(x => x.PunchedAt)).ThenBy(g => g.Key.CardNumber)
            .Select(g => new UnattributedCardDto(g.Key.CardNumber, g.Key.CardId, g.Count(), g.Min(x => x.PunchedAt), g.Max(x => x.PunchedAt)))
            .ToListAsync(ct));
    }
}
