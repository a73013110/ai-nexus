using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Staff.Attendance.Cards;

/// <summary>
/// 修改持有期間：更正起迄、備註，或把只有姓名的持有人綁上 <c>UserId</c>（<c>HolderName</c> 保留）。不能換卡；
/// 結束持有就是填上 <c>EndsAt</c>。
/// </summary>
public sealed record CardAssignmentUpdateRequest(Guid? UserId, string? HolderName, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string? Note) : IAssignmentFields;

internal sealed class CardAssignmentUpdateRequestValidator : RequestValidator<CardAssignmentUpdateRequest>
{
    public CardAssignmentUpdateRequestValidator() => this.AddFieldRules();
}

/// <summary>
/// 修改持有期間（會稽核，<c>admin.attendance_assignment</c>）。新的期間與同一張卡的其他期間重疊一律拒絕，沒有接手：
/// 接手是「指派給新的人」的語意，更正既有紀錄時重疊代表輸入有誤，不該悄悄改動另一段。
/// 綁上使用者後，該使用者在這段期間內的刷卡立即歸戶（view 在讀取時計算，不必回頭改寫刷卡）。
/// </summary>
internal sealed class UpdateCardAssignment(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}", (Guid id, CardAssignmentUpdateRequest body, ICurrentUser user, UpdateCardAssignment handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, body, ct).ToHttpResultAsync())
        .RequireAuthorization(Policies.AttendanceCardsMaintain)
        .WithName("UpdateAttendanceCardAssignment");

    public async Task<Result> HandleAsync(Guid actor, Guid id, CardAssignmentUpdateRequest request, CancellationToken ct)
    {
        // 稽核快照以整張卡為單位，所以先查出它屬於哪張卡；查不到就讓交易內的檢查回 not found 並留下紀錄。
        var cardId = await db.Set<CardAssignment>().AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.CardId).SingleOrDefaultAsync(ct);
        return await audit.MutateAsync(actor, FeatureCatalog.AttendanceCardsMaintain, "admin.attendance_assignment", id, id.ToString(), token => AssignmentRules.SnapshotAsync(db, cardId, token), async () =>
        {
            var assignment = await db.Set<CardAssignment>().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (assignment is null) return AttendanceErrors.AssignmentNotFound;
            // 沒換人就不重查：持有人帳號之後被刪除，不該讓更正備註或結束時間的操作失敗。
            if (request.UserId is { } userId && userId != assignment.UserId && !await db.Users.AnyAsync(x => x.Id == userId && x.DeletedAt == null, ct))
                return AttendanceErrors.UserNotFound;
            var others = await db.Set<CardAssignment>().AsNoTracking().Where(x => x.CardId == assignment.CardId && x.Id != id).ToListAsync(ct);
            if (others.Any(x => AssignmentRules.Overlaps(request.StartsAt, request.EndsAt, x))) return AttendanceErrors.AssignmentOverlap;
            assignment.UserId = request.UserId;
            assignment.HolderName = CardRules.Normalize(request.HolderName);
            assignment.StartsAt = request.StartsAt;
            assignment.EndsAt = request.EndsAt;
            assignment.Note = CardRules.Normalize(request.Note);
            return Result.Success;
        }, ct);
    }
}
