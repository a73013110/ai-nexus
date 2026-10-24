using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Cards;

/// <summary>
/// 指派卡號。持有人是 <c>UserId</c> 或 <c>HolderName</c>（還沒有帳號的新進員工，之後用修改綁上 <c>UserId</c>），至少一個；
/// <c>EndsAt</c> 為 null 表示持有中。卡上已有人持有時，必須帶 <c>TakeOver</c> 才會把前一段結束在新段的 <c>StartsAt</c>。
/// </summary>
public sealed record CardAssignmentCreateRequest(
    Guid CardId, Guid? UserId, string? HolderName, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string? Note, bool TakeOver = false) : IAssignmentFields;

internal sealed class CardAssignmentCreateRequestValidator : RequestValidator<CardAssignmentCreateRequest>
{
    public CardAssignmentCreateRequestValidator()
    {
        RuleFor(x => x.CardId).NotEmpty();
        this.AddFieldRules();
    }
}

/// <summary>
/// 新增持有期間（會稽核，<c>admin.attendance_assignment</c>）。規則見 <see cref="AssignmentRules.Place"/>；
/// 檢查與寫入在 <c>AdministrativeAudit</c> 的 Serializable 交易與行政寫入鎖內，兩個人同時指派同一張卡時後到的會看到先到的結果，不會雙雙通過。
/// 被拒絕的 <c>attendance_card_held</c> 不帶目前的持有人（錯誤回應不帶資料）：前端由持有歷史得知是誰持有，提示後以 <c>TakeOver</c> 重送。
/// </summary>
internal sealed class CreateCardAssignment(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("", (CardAssignmentCreateRequest body, ICurrentUser user, CreateCardAssignment handler, CancellationToken ct) => handler.HandleAsync(user.Id, body, ct).ToHttpResultAsync())
        .RequireAuthorization(Policies.AttendanceCardsMaintain)
        .WithName("CreateAttendanceCardAssignment");

    public async Task<Result<CardAssignmentDto>> HandleAsync(Guid actor, CardAssignmentCreateRequest request, CancellationToken ct)
    {
        var assignment = new CardAssignment
        {
            CardId = request.CardId,
            UserId = request.UserId,
            HolderName = CardRules.Normalize(request.HolderName),
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            Note = CardRules.Normalize(request.Note),
        };
        var outcome = await audit.MutateAsync(actor, FeatureCatalog.AttendanceCardsMaintain, "admin.attendance_assignment", assignment.Id, assignment.Id.ToString(), token => AssignmentRules.SnapshotAsync(db, request.CardId, token), async () =>
        {
            var kind = await db.Set<Card>().Where(x => x.Id == request.CardId).Select(x => x.Kind).SingleOrDefaultAsync(ct);
            if (kind is null) return AttendanceErrors.CardNotFound;
            if (kind == CardKinds.Visitor) return AttendanceErrors.CardIsVisitor;
            if (request.UserId is { } userId && !await AssignmentRules.HolderExistsAsync(db, userId, ct)) return AttendanceErrors.UserNotFound;
            // 追蹤中的實體：接手時 Place 改掉前一段的 EndsAt，與新段在同一次 SaveChanges 寫入、同一筆稽核。
            var existing = await db.Set<CardAssignment>().Where(x => x.CardId == request.CardId).ToListAsync(ct);
            if (AssignmentRules.Place(existing, assignment, request.TakeOver) is { } refused) return refused;
            db.Add(assignment);
            return Result.Success;
        }, ct);
        if (!outcome.IsSuccess) return outcome.Error;
        return await db.Set<CardAssignment>().AsNoTracking().Where(x => x.Id == assignment.Id).ToDtos(db).SingleAsync(ct);
    }
}
