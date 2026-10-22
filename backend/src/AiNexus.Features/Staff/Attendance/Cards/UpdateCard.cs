using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Staff.Attendance.Cards;

/// <summary>修改卡號、種類與備註。</summary>
public sealed record CardUpdateRequest(string CardNumber, string Kind, string? Note);

internal sealed class CardUpdateRequestValidator : RequestValidator<CardUpdateRequest>
{
    public CardUpdateRequestValidator()
    {
        RuleFor(x => x.CardNumber).CardNumber();
        RuleFor(x => x.Kind).Kind();
        RuleFor(x => x.Note).Note();
    }
}

/// <summary>
/// 修改卡片。改卡號會讓刷卡的歸戶隨之改變（view 以卡號字串比對），用於更正登錄錯誤，所以前後值都在稽核快照裡；
/// 已有持有期間的員工卡不能改成訪客卡，否則既有的歸戶會靜默失效。會稽核（<c>admin.attendance_card</c>）。
/// </summary>
internal sealed class UpdateCard(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}", (Guid id, CardUpdateRequest body, ICurrentUser user, UpdateCard handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, body, ct).ToHttpResultAsync())
        .RequireAuthorization(Policies.AttendanceCardsMaintain)
        .WithName("UpdateAttendanceCard");

    public Task<Result> HandleAsync(Guid actor, Guid id, CardUpdateRequest request, CancellationToken ct) => audit.MutateAsync(actor, FeatureCatalog.AttendanceCardsMaintain, "admin.attendance_card", id, id.ToString(), token => CardRules.SnapshotAsync(db, id, token), async () =>
    {
        var card = await db.Set<Card>().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (card is null) return AttendanceErrors.CardNotFound;
        var number = request.CardNumber.Trim();
        if (await db.Set<Card>().AnyAsync(x => x.Id != id && x.CardNumber == number, ct)) return AttendanceErrors.CardNumberDuplicate;
        if (request.Kind == CardKinds.Visitor && card.Kind != CardKinds.Visitor && await db.Set<CardAssignment>().AnyAsync(x => x.CardId == id, ct))
            return AttendanceErrors.CardHasAssignments;
        card.CardNumber = number;
        card.Kind = request.Kind;
        card.Note = CardRules.Normalize(request.Note);
        return Result.Success;
    }, ct);
}
