using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Cards;

/// <summary>新增卡片。卡號去掉前後空白後必須唯一（不分大小寫）；<c>HiddenFromRecords</c> 見 <see cref="Card.HiddenFromRecords"/>。</summary>
public sealed record CardCreateRequest(string CardNumber, bool HiddenFromRecords, string? Note);

/// <summary>
/// 卡號是打卡檔裡的原始字串（不補零、不轉數字），去掉前後空白後存入，之後與刷卡以相同字串比對。
/// </summary>
internal sealed class CardCreateRequestValidator : RequestValidator<CardCreateRequest>
{
    public CardCreateRequestValidator()
    {
        RuleFor(x => x.CardNumber).RequiredText(Card.CardNumberMaxLength);
        RuleFor(x => x.Note).OptionalText(Card.NoteMaxLength);
    }
}

/// <summary>
/// 新增還沒刷過的卡（例如發給新人前先指派）；刷過的卡號在匯入時已自動建立。會稽核（<c>admin.attendance_card</c>）。
/// </summary>
internal sealed class CreateCard(AppDbContext db, AdministrativeWrite write)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("", (CardCreateRequest body, CreateCard handler, CancellationToken ct) =>
            handler.HandleAsync(body, ct))
        .RequireFeature(FeatureCatalog.AttendanceCardsMaintain)
        .Audited(AttendanceAudit.CardCreated)
        .WithName("CreateAttendanceCard");

    public async Task<Result<CardDto>> HandleAsync(CardCreateRequest request, CancellationToken ct)
    {
        var card = new Card { CardNumber = request.CardNumber.Trim(), HiddenFromRecords = request.HiddenFromRecords, Note = request.Note.TrimToNull() };
        var begun = await write.BeginAsync(CardRules.Target(db, card.Id).Locking(AttendanceLocks.PunchImport), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        // 唯一索引是最後一道防線；先查才能回固定的錯誤代碼並寫進稽核。
        if (await db.Cards.AnyAsync(x => x.CardNumber == card.CardNumber, ct)) return AttendanceErrors.CardNumberDuplicate;
        db.Add(card);

        var committed = await transaction.CommitAsync(ct);
        return committed.IsSuccess ? CardDto.New(card) : committed.Error;
    }
}
