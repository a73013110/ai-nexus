using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Cards;

/// <summary>
/// 刪除持有期間，用於輸入錯誤的紀錄（換人請用接手，結束持有請改 <c>EndsAt</c>）。刪除後該期間內的刷卡變成未歸戶，
/// 刷卡原始資料不動。會稽核（<c>admin.attendance_assignment_delete</c>），快照是該卡刪除前後的全部期間。
/// </summary>
internal sealed class DeleteCardAssignment(AppDbContext db, AdministrativeWrite write) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", (Guid id, long rowVersion, DeleteCardAssignment handler, CancellationToken ct) =>
            handler.HandleAsync(id, rowVersion, ct))
        .RequireFeature(FeatureCatalog.AttendanceCardsMaintain)
        .Audited(AttendanceAudit.AssignmentDeleted)
        .WithName("DeleteAttendanceCardAssignment");

    public async Task<Result> HandleAsync(Guid id, long rowVersion, CancellationToken ct)
    {
        var cardId = await AssignmentRules.CardOfAsync(db, id, ct);
        var begun = await write.BeginAsync(AssignmentRules.Target(db, id, cardId), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        var assignment = await db.CardAssignments.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (assignment is null) return AttendanceErrors.AssignmentNotFound;
        db.ExpectRowVersion(assignment, rowVersion);
        db.Remove(assignment);

        return await transaction.CommitAsync(ct);
    }
}
