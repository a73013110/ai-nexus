using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Cards;

/// <summary>卡片與持有期間共用的文字正規化，以及卡片的稽核快照。</summary>
internal static class CardRules
{
    /// <summary>空白存成 null。</summary>
    public static string? Normalize(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>新增、修改與刪除卡片共用的稽核快照（<c>admin.attendance_card*</c>）；卡不存在時為 null。備註不進稽核，所以不讀。</summary>
    public static async Task<object?> SnapshotAsync(AppDbContext db, Guid id, CancellationToken ct)
        => await db.Set<Card>().AsNoTracking().Where(x => x.Id == id).Select(x => new { x.CardNumber, x.HiddenFromRecords }).SingleOrDefaultAsync(ct);
}
