using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Cards;

/// <summary>
/// 刪除登錄錯誤的卡片。只允許沒有任何持有期間的卡：持有期間是歸戶的依據，連同卡片一起刪會讓歷史刷卡全部變成未歸戶，
/// 外鍵也不級聯；不再使用的卡把持有期間結束即可。刷卡原始資料不受影響（它們不參照卡片）。會稽核（<c>admin.attendance_card_delete</c>）。
/// </summary>
internal sealed class DeleteCard(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", (Guid id, ICurrentUser user, DeleteCard handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .RequireAuthorization(Policies.AttendanceCardsMaintain)
        .WithName("DeleteAttendanceCard");

    public Task<Result> HandleAsync(Guid actor, Guid id, CancellationToken ct) => audit.MutateAsync(actor, FeatureCatalog.AttendanceCardsMaintain, "admin.attendance_card_delete", id, id.ToString(), token => CardRules.SnapshotAsync(db, id, token), async () =>
    {
        var card = await db.Set<Card>().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (card is null) return AttendanceErrors.CardNotFound;
        if (await db.Set<CardAssignment>().AnyAsync(x => x.CardId == id, ct)) return AttendanceErrors.CardHasAssignments;
        db.Remove(card);
        return Result.Success;
    }, ct);
}
