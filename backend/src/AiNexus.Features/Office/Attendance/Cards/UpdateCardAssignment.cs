using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Cards;

/// <summary>
/// 修改持有期間：更正起迄、備註，或把只有姓名的持有人綁上 <c>UserId</c>（<c>HolderName</c> 保留）。不能換卡；
/// 結束持有就是填上 <c>EndsAt</c>。
/// </summary>
public sealed record CardAssignmentUpdateRequest(Guid? UserId, string? HolderName, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string? Note, long RowVersion) : IAssignmentFields;

internal sealed class CardAssignmentUpdateRequestValidator : RequestValidator<CardAssignmentUpdateRequest>
{
    public CardAssignmentUpdateRequestValidator()
    {
        this.AddFieldRules();
        RuleFor(x => x.RowVersion).GreaterThan(0).WithErrorCode(RuleCode.Required);
    }
}

/// <summary>
/// 修改持有期間（會稽核，<c>admin.attendance_assignment</c>）。新的期間與同一張卡的其他期間重疊一律拒絕，沒有接手：
/// 接手是「指派給新的人」的語意，更正既有紀錄時重疊代表輸入有誤，不該悄悄改動另一段。
/// 綁上使用者後，該使用者在這段期間內的刷卡立即歸戶（view 在讀取時計算，不必回頭改寫刷卡）。
/// </summary>
internal sealed class UpdateCardAssignment(AppDbContext db, AdministrativeWrite write)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}", (Guid id, CardAssignmentUpdateRequest body, UpdateCardAssignment handler, CancellationToken ct) =>
            handler.HandleAsync(id, body, ct))
        .RequireFeature(FeatureCatalog.AttendanceCardsMaintain)
        .Audited(AttendanceAudit.AssignmentUpdated)
        .WithName("UpdateAttendanceCardAssignment");

    public async Task<Result> HandleAsync(Guid id, CardAssignmentUpdateRequest request, CancellationToken ct)
    {
        // 查不到時快照為 null，交給交易內的檢查回 not found 並留下紀錄。期間不能換卡，所以鎖外查到的卡號在鎖內仍然成立。
        var cardId = await AssignmentRules.CardOfAsync(db, id, ct);
        var begun = await write.BeginAsync(AssignmentRules.Target(db, id, cardId), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        var assignment = await db.CardAssignments.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (assignment is null) return AttendanceErrors.AssignmentNotFound;
        // 沒換人就不重查：持有人帳號之後被刪除，不該讓更正備註或結束時間的操作失敗。
        if (request.UserId is { } userId && userId != assignment.UserId && !await AssignmentRules.HolderExistsAsync(db, userId, ct))
            return AttendanceErrors.UserNotFound;
        var others = await db.CardAssignments.AsNoTracking().Where(x => x.CardId == assignment.CardId && x.Id != id).ToListAsync(ct);
        if (others.Any(x => AssignmentRules.Overlaps(request.StartsAt, request.EndsAt, x))) return AttendanceErrors.AssignmentOverlap;
        db.ExpectRowVersion(assignment, request.RowVersion);
        assignment.UserId = request.UserId;
        assignment.HolderName = request.HolderName.TrimToNull();
        assignment.StartsAt = request.StartsAt;
        assignment.EndsAt = request.EndsAt;
        assignment.Note = request.Note.TrimToNull();

        return await transaction.CommitAsync(ct);
    }
}
