using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Cards;

/// <summary>
/// 卡號對應頁的卡片清單，依卡號排序，附上現在的持有期間。<c>search</c> 比對卡號、備註，以及任何一段持有期間的持有人姓名或使用者名稱
/// （HR 常從人名找卡）；<c>kind</c> 限定員工卡或訪客卡。卡片由 HR 逐張登錄、數量以百計，所以不分頁。
/// </summary>
internal sealed class ListCards(AppDbContext db, TimeProvider clock)
{
    private const int SearchMaxLength = 100;

    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("", (string? search, string? kind, ListCards handler, CancellationToken ct) => handler.HandleAsync(search, kind, ct).ToHttpResultAsync())
        .WithName("ListAttendanceCards");

    public async Task<Result<IReadOnlyList<CardDto>>> HandleAsync(string? search, string? kind, CancellationToken ct)
    {
        if (kind is not null && !CardKinds.IsDefined(kind)) return AttendanceErrors.FilterInvalid;
        var query = db.Set<Card>().AsNoTracking();
        if (kind is not null) query = query.Where(x => x.Kind == kind);
        if (search?.Trim() is { Length: > 0 } text)
        {
            if (text.Length > SearchMaxLength) text = text[..SearchMaxLength];
            query = query.Where(c => c.CardNumber.Contains(text) || (c.Note != null && c.Note.Contains(text))
                || db.Set<CardAssignment>().Any(a => a.CardId == c.Id
                    && ((a.HolderName != null && a.HolderName.Contains(text)) || (a.UserId != null && db.Users.Any(u => u.Id == a.UserId && u.DisplayName.Contains(text))))));
        }
        var cards = await query.OrderBy(x => x.CardNumber).ToListAsync(ct);

        var ids = cards.Select(x => x.Id).ToList();
        var current = await db.Set<CardAssignment>().AsNoTracking().Where(a => ids.Contains(a.CardId)).CoveringAt(clock.GetUtcNow()).ToDtos(db).ToListAsync(ct);
        // 同一張卡的期間不重疊，正常只有一段；壞資料時取開始最晚的那段，不讓清單失敗。
        var byCard = current.GroupBy(x => x.CardId).ToDictionary(x => x.Key, x => x.MaxBy(y => y.StartsAt));
        return Result<IReadOnlyList<CardDto>>.Ok([.. cards.Select(x => CardDto.From(x, byCard.GetValueOrDefault(x.Id)))]);
    }
}
