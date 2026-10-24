using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Cards;

/// <summary>卡號對應頁的狀態篩選；不帶表示全部。</summary>
public static class CardStatuses
{
    /// <summary>待指派：有沒有持有期間涵蓋的刷卡，而且沒有隱藏（隱藏的卡是 HR 刻意不管的，例如訪客卡）。</summary>
    public const string Unassigned = "unassigned";

    /// <summary>現在有人持有。</summary>
    public const string Held = "held";

    /// <summary>在出勤紀錄隱藏。</summary>
    public const string Hidden = "hidden";

    public static bool IsDefined(string? status) => status is Unassigned or Held or Hidden;
}

/// <summary>
/// 卡號對應頁的卡片清單：打卡檔出現過的卡號都在 <see cref="Card"/>（匯入時建立），所以一張表就涵蓋已指派與待指派的卡。
/// <c>search</c> 比對卡號、備註，以及任何一段持有期間的持有人姓名或使用者名稱（HR 常從人名找卡）；<c>status</c> 見 <see cref="CardStatuses"/>。
/// 一律依卡號排序（HR 是拿著卡號來找卡的，順序不隨狀態改變）。卡片數以百計，不分頁。
/// </summary>
internal sealed class ListCards(AppDbContext db, TimeProvider clock)
{
    private const int SearchMaxLength = 100;

    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("", (string? search, string? status, ListCards handler, CancellationToken ct) => handler.HandleAsync(search, status, ct).ToHttpResultAsync())
        .WithName("ListAttendanceCards");

    public async Task<Result<IReadOnlyList<CardDto>>> HandleAsync(string? search, string? status, CancellationToken ct)
    {
        if (status is not null && !CardStatuses.IsDefined(status)) return AttendanceErrors.FilterInvalid;
        var now = clock.GetUtcNow();
        var assignments = db.Set<CardAssignment>();
        var unattributed = db.Set<AttributedPunch>().Where(p => p.IsUnattributed);
        var query = status switch
        {
            CardStatuses.Unassigned => db.Set<Card>().Where(c => !c.HiddenFromRecords && unattributed.Any(p => p.CardId == c.Id)),
            CardStatuses.Held => db.Set<Card>().Where(c => assignments.Any(a => a.CardId == c.Id && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))),
            CardStatuses.Hidden => db.Set<Card>().Where(c => c.HiddenFromRecords),
            _ => db.Set<Card>(),
        };
        if (search?.Trim() is { Length: > 0 } text)
        {
            if (text.Length > SearchMaxLength) text = text[..SearchMaxLength];
            query = query.Where(c => c.CardNumber.Contains(text) || (c.Note != null && c.Note.Contains(text))
                || assignments.Any(a => a.CardId == c.Id
                    && ((a.HolderName != null && a.HolderName.Contains(text)) || (a.UserId != null && db.Users.Any(u => u.Id == a.UserId && u.DisplayName.Contains(text))))));
        }
        var cards = await query.AsNoTracking().OrderBy(x => x.CardNumber).ToListAsync(ct);
        if (cards.Count == 0) return Result<IReadOnlyList<CardDto>>.Ok([]);

        // 其餘三次查詢都以同一個篩選當子查詢，不把整份卡號清單展開成參數。
        var ids = query.Select(c => c.Id);
        var numbers = query.Select(c => c.CardNumber);
        var current = await assignments.AsNoTracking().Where(a => ids.Contains(a.CardId)).CoveringAt(now).ToDtos(db).ToListAsync(ct);
        // 同一張卡的期間不重疊，正常只有一段；壞資料時取開始最晚的那段，不讓清單失敗。
        var holders = current.GroupBy(x => x.CardId).ToDictionary(x => x.Key, x => x.MaxBy(y => y.StartsAt));
        // 卡號比對與資料庫定序一致（不分大小寫）。
        var lastPunches = await db.Set<Punch>().Where(p => numbers.Contains(p.CardNumber))
            .GroupBy(p => p.CardNumber).Select(g => new { g.Key, Last = g.Max(p => p.PunchedAt) })
            .ToDictionaryAsync(x => x.Key, x => x.Last, StringComparer.OrdinalIgnoreCase, ct);
        var pending = await unattributed.Where(p => p.CardId != null && ids.Contains(p.CardId.Value))
            .GroupBy(p => p.CardId!.Value).Select(g => new { g.Key, Count = g.Count(), First = g.Min(p => p.PunchedAt), Last = g.Max(p => p.PunchedAt) })
            .ToDictionaryAsync(x => x.Key, ct);

        var rows = cards.Select(card =>
        {
            var gap = pending.GetValueOrDefault(card.Id);
            return new CardDto(card.Id, card.CardNumber, card.HiddenFromRecords, card.Note, card.CreatedAt, holders.GetValueOrDefault(card.Id),
                lastPunches.TryGetValue(card.CardNumber, out var last) ? last : null, gap?.Count ?? 0, gap?.First, gap?.Last);
        });
        return Result<IReadOnlyList<CardDto>>.Ok([.. rows]);
    }
}
