using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Cards;

/// <summary>修改卡片的隱藏與備註；卡號建立後不改（刷卡以卡號字串對應，改了既有刷卡就對不到）。</summary>
public sealed record CardUpdateRequest(bool HiddenFromRecords, string? Note, long RowVersion);

internal sealed class CardUpdateRequestValidator : RequestValidator<CardUpdateRequest>
{
    public CardUpdateRequestValidator()
    {
        RuleFor(x => x.Note).OptionalText(Card.NoteMaxLength);
        RuleFor(x => x.RowVersion).GreaterThan(0).WithErrorCode(RuleCode.Required);
    }
}

/// <summary>
/// 修改是否在出勤紀錄隱藏與備註。隱藏只影響 HR 看全公司的出勤紀錄（見 <see cref="Card.HiddenFromRecords"/>），持有期間與刷卡不動。
/// 只動一列，由 rowversion 防止覆蓋別人的修改，不需要鎖。會稽核（<c>admin.attendance_card</c>），快照含前後的隱藏旗標；備註不進稽核。
/// </summary>
internal sealed class UpdateCard(AppDbContext db, AdministrativeWrite write)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}", (Guid id, CardUpdateRequest body, UpdateCard handler, CancellationToken ct) =>
            handler.HandleAsync(id, body, ct))
        .RequireFeature(FeatureCatalog.AttendanceCardsMaintain)
        .Audited(AttendanceAudit.CardUpdated)
        .WithName("UpdateAttendanceCard");

    public async Task<Result> HandleAsync(Guid id, CardUpdateRequest request, CancellationToken ct)
    {
        var begun = await write.BeginAsync(CardRules.Target(db, id), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        var card = await db.Cards.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (card is null) return AttendanceErrors.CardNotFound;
        db.ExpectRowVersion(card, request.RowVersion);
        card.HiddenFromRecords = request.HiddenFromRecords;
        card.Note = request.Note.TrimToNull();

        return await transaction.CommitAsync(ct);
    }
}
