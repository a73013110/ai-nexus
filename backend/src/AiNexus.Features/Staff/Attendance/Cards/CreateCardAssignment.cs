using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Staff.Attendance.Cards;

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
/// 卡上已有人持有、而請求沒有要求接手時的 409：除了錯誤代碼，還帶目前的持有期間，讓前端提示「這張卡現在是某某持有，要接手嗎」。
/// 通用的錯誤回應不能帶資料（只有代碼與欄位規則），所以這個端點多宣告一種 409 回應；<c>Code</c>、<c>Status</c>、<c>Title</c> 與一般錯誤同名同義。
/// </summary>
public sealed record CardHeldResponse(string Code, int Status, string Title, CardAssignmentDto Current);

/// <summary>
/// 新增持有期間（會稽核，<c>admin.attendance_assignment</c>）。規則見 <see cref="AssignmentRules.Place"/>；
/// 檢查與寫入在 <c>AdministrativeAudit</c> 的 Serializable 交易與行政寫入鎖內，兩個人同時指派同一張卡時後到的會看到先到的結果，不會雙雙通過。
/// 接手對前一段的修改與新段一起寫入、一起稽核（快照是該卡的全部期間）。
/// </summary>
internal sealed class CreateCardAssignment(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("", (CardAssignmentCreateRequest body, ICurrentUser user, CreateCardAssignment handler, CancellationToken ct) => handler.HandleHttpAsync(user.Id, body, ct))
        .RequireAuthorization(Policies.AttendanceCardsMaintain)
        .WithName("CreateAttendanceCardAssignment");

    public async Task<Results<Ok<CardAssignmentDto>, Conflict<CardHeldResponse>, ProblemHttpResult>> HandleHttpAsync(Guid actor, CardAssignmentCreateRequest request, CancellationToken ct)
    {
        var result = await HandleAsync(actor, request, ct);
        if (result.IsSuccess) return TypedResults.Ok(result.Value);
        if (result.Error == AttendanceErrors.CardHeld && await HolderAtAsync(request, ct) is { } current)
            return TypedResults.Conflict(new CardHeldResponse(result.Error.Code, Problems.Status(result.Error.Kind), PublicErrorCatalog.Message(result.Error.Code, Problems.Status(result.Error.Kind)), current));
        return result.Error.ToProblem();
    }

    public async Task<Result<CardAssignmentDto>> HandleAsync(Guid actor, CardAssignmentCreateRequest request, CancellationToken ct)
    {
        var assignment = new CardAssignment
        {
            Id = Guid.NewGuid(),
            CardId = request.CardId,
            UserId = request.UserId,
            HolderName = CardRules.Normalize(request.HolderName),
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            Note = CardRules.Normalize(request.Note),
        };
        var outcome = await audit.MutateAsync(actor, FeatureCatalog.AttendanceCardsMaintain, "admin.attendance_assignment", assignment.Id, assignment.Id.ToString(), token => AssignmentRules.SnapshotAsync(db, request.CardId, token), async () =>
        {
            var card = await db.Set<Card>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.CardId, ct);
            if (card is null) return AttendanceErrors.CardNotFound;
            if (card.Kind == CardKinds.Visitor) return AttendanceErrors.CardIsVisitor;
            if (request.UserId is { } userId && !await db.Users.AnyAsync(x => x.Id == userId && x.DeletedAt == null, ct)) return AttendanceErrors.UserNotFound;
            // 被接手的前一段是追蹤中的實體，改它的 EndsAt 會與新段在同一次 SaveChanges 寫入。
            var existing = await db.Set<CardAssignment>().Where(x => x.CardId == request.CardId).ToListAsync(ct);
            var placement = AssignmentRules.Place(existing, assignment.StartsAt, assignment.EndsAt, request.TakeOver);
            if (placement.Error is { } error) return error;
            if (placement.EndsPrevious is { } previous) previous.EndsAt = assignment.StartsAt;
            db.Add(assignment);
            return Result.Success;
        }, ct);
        if (!outcome.IsSuccess) return outcome.Error;
        return await db.Set<CardAssignment>().AsNoTracking().Where(x => x.Id == assignment.Id).ToDtos(db).SingleAsync(ct);
    }

    // 只用來回覆前端；稽核已經留下這次被拒絕的嘗試，這裡不必在交易內。
    private Task<CardAssignmentDto?> HolderAtAsync(CardAssignmentCreateRequest request, CancellationToken ct)
        => db.Set<CardAssignment>().AsNoTracking()
            .Where(x => x.CardId == request.CardId && x.StartsAt <= request.StartsAt && (x.EndsAt == null || x.EndsAt > request.StartsAt))
            .ToDtos(db).FirstOrDefaultAsync(ct);
}
