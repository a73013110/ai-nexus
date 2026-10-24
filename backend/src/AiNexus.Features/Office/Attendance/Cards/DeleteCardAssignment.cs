using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Cards;

/// <summary>
/// 刪除持有期間，用於輸入錯誤的紀錄（換人請用接手，結束持有請改 <c>EndsAt</c>）。刪除後該期間內的刷卡變成未歸戶，
/// 刷卡原始資料不動。會稽核（<c>admin.attendance_assignment_delete</c>），快照是該卡刪除前後的全部期間。
/// </summary>
internal sealed class DeleteCardAssignment(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", (Guid id, ICurrentUser user, DeleteCardAssignment handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .RequireAuthorization(Policies.AttendanceCardsMaintain)
        .WithName("DeleteAttendanceCardAssignment");

    public async Task<Result> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var cardId = await AssignmentRules.CardOfAsync(db, id, ct);
        return await audit.MutateAsync(actor, FeatureCatalog.AttendanceCardsMaintain, "admin.attendance_assignment_delete", id, id.ToString(), token => AssignmentRules.SnapshotAsync(db, cardId, token), async () =>
        {
            var assignment = await db.Set<CardAssignment>().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (assignment is null) return AttendanceErrors.AssignmentNotFound;
            db.Remove(assignment);
            return Result.Success;
        }, ct);
    }
}
