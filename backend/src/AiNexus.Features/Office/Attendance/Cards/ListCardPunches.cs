using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Office.Attendance.Punches;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Data.Tables;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Cards;

/// <summary>
/// 卡片詳情的刷卡紀錄，預設新到舊；排序、欄位篩選與分頁走共用的 <see cref="TableRequest"/>（ADR 0026），與匯入明細同一套欄位。
/// 出勤紀錄以人為主、會判定遲到早退；這裡以卡片為主，只列原始刷卡與歸戶結果，讓 HR 查這張卡在誰手上時刷過哪些。
/// 刷卡依卡號字串對到卡片（與歸戶 view 同一個 join），所以卡片建立前就刷過的也會列出。
/// </summary>
internal sealed class ListCardPunches(AppDbContext db)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/punches", (Guid id, [AsParameters] TableRequest table, ListCardPunches handler, CancellationToken ct)
            => handler.HandleAsync(id, table, ct).ToHttpResultAsync())
        .Audited(AttendanceAudit.PunchRead)
        .WithName("ListAttendanceCardPunches");

    public async Task<Result<TablePage<PunchDto>>> HandleAsync(Guid id, TableRequest table, CancellationToken ct)
    {
        if (!await db.Set<Card>().AnyAsync(x => x.Id == id, ct)) return AttendanceErrors.CardNotFound;
        var punches = db.Set<AttributedPunch>().Where(x => x.CardId == id);
        return await punches.ToTablePageAsync(table, db.PunchColumns().DefaultSort("punchedAt", descending: true), x => x.ToDtos(db), ct);
    }
}
