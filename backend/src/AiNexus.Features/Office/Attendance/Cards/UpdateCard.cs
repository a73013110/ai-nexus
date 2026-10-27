using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Cards;

/// <summary>修改卡片的隱藏與備註；卡號建立後不改（刷卡以卡號字串對應，改了既有刷卡就對不到）。</summary>
public sealed record CardUpdateRequest(bool HiddenFromRecords, string? Note);

internal sealed class CardUpdateRequestValidator : RequestValidator<CardUpdateRequest>
{
    public CardUpdateRequestValidator() => RuleFor(x => x.Note).OptionalText(Card.NoteMaxLength);
}

/// <summary>
/// 修改是否在出勤紀錄隱藏與備註。隱藏只影響 HR 看全公司的出勤紀錄（見 <see cref="Card.HiddenFromRecords"/>），持有期間與刷卡不動。
/// 會稽核（<c>admin.attendance_card</c>），快照含前後的隱藏旗標；備註不進稽核。
/// </summary>
internal sealed class UpdateCard(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}", (Guid id, CardUpdateRequest body, ICurrentUser user, UpdateCard handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, body, ct).ToHttpResultAsync())
        .RequireAuthorization(Policies.AttendanceCardsMaintain)
        .Audited(AttendanceAudit.CardUpdated)
        .WithName("UpdateAttendanceCard");

    public Task<Result> HandleAsync(Guid actor, Guid id, CardUpdateRequest request, CancellationToken ct) => audit.MutateAsync(actor, FeatureCatalog.AttendanceCardsMaintain, id, id.ToString(), token => CardRules.SnapshotAsync(db, id, token), async () =>
    {
        var card = await db.Set<Card>().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (card is null) return AttendanceErrors.CardNotFound;
        card.HiddenFromRecords = request.HiddenFromRecords;
        card.Note = CardRules.Normalize(request.Note);
        return Result.Success;
    }, ct);
}
