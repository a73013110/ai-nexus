using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Cards;

/// <summary>卡片行政寫入共用的宣告。</summary>
internal static class CardRules
{
    /// <summary>新增、修改與刪除卡片共用的寫入對象（稽核 <c>admin.attendance_card*</c>）。卡不存在時快照為 null；備註不進稽核，所以不讀。</summary>
    public static AuditTarget Target(AppDbContext db, Guid id) => AuditTarget.Of(id, async ct =>
        await db.Cards.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.CardNumber, x.HiddenFromRecords }).SingleOrDefaultAsync(ct));
}
