using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Cards;

/// <summary>
/// 刪除手動建立錯誤、從沒用過的卡。有持有期間（歸戶依據，外鍵不級聯）或刷卡（下次匯入又會建回來，期間的刷卡也會從清單消失）時拒絕；
/// 不再使用的卡把持有期間結束即可，不想在出勤紀錄看到的改為隱藏。會稽核（<c>admin.attendance_card_delete</c>）。
/// </summary>
internal sealed class DeleteCard(AppDbContext db, AdministrativeWrite write)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", (Guid id, ICurrentUser user, DeleteCard handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct))
        .RequireFeature(FeatureCatalog.AttendanceCardsMaintain)
        .Audited(AttendanceAudit.CardDeleted)
        .WithName("DeleteAttendanceCard");

    public Task<Result> HandleAsync(Guid actor, Guid id, CancellationToken ct) => write.ExecuteAsync(actor, CardRules.Target(db, id), async () =>
    {
        var card = await db.Cards.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (card is null) return AttendanceErrors.CardNotFound;
        if (await db.CardAssignments.AnyAsync(x => x.CardId == id, ct) || await db.Punches.AnyAsync(x => x.CardNumber == card.CardNumber, ct))
            return AttendanceErrors.CardInUse;
        db.Remove(card);
        return Result.Success;
    }, ct);
}
