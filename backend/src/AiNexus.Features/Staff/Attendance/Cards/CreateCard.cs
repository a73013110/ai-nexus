using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Staff.Attendance.Cards;

/// <summary>登錄或修改卡片。<c>Kind</c> 是 <see cref="CardKinds"/> 之一；卡號去掉前後空白後必須唯一（不分大小寫）。</summary>
public sealed record CardRequest(string CardNumber, string Kind, string? Note);

/// <summary>
/// 卡號是打卡檔裡的原始字串（不補零、不轉數字），去掉前後空白後存入，之後與刷卡以相同字串比對。
/// 控制字元會讓打卡檔的逐行解析與畫面顯示出錯，所以擋掉。
/// </summary>
internal sealed class CardRequestValidator : RequestValidator<CardRequest>
{
    public CardRequestValidator()
    {
        RuleFor(x => x.CardNumber).Must(x => !string.IsNullOrWhiteSpace(x)).WithErrorCode("required")
            .Must(x => x is null || x.Trim().Length <= Card.CardNumberMaxLength).WithErrorCode("too_long")
            .Must(x => x is null || !x.Any(char.IsControl)).WithErrorCode("format");
        RuleFor(x => x.Kind).Must(CardKinds.IsDefined).WithErrorCode("unknown");
        RuleFor(x => x.Note).OptionalText(Card.NoteMaxLength);
    }
}

/// <summary>登錄卡片。會稽核（<c>admin.attendance_card</c>）。</summary>
internal sealed class CreateCard(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("", (CardRequest body, ICurrentUser user, CreateCard handler, CancellationToken ct) => handler.HandleAsync(user.Id, body, ct).ToHttpResultAsync())
        .RequireAuthorization(Policies.AttendanceCardsMaintain)
        .WithName("CreateAttendanceCard");

    public async Task<Result<CardDto>> HandleAsync(Guid actor, CardRequest request, CancellationToken ct)
    {
        var card = new Card { CardNumber = request.CardNumber.Trim(), Kind = request.Kind, Note = CardRules.Normalize(request.Note) };
        var outcome = await audit.MutateAsync(actor, FeatureCatalog.AttendanceCardsMaintain, "admin.attendance_card", card.Id, card.Id.ToString(), token => CardRules.SnapshotAsync(db, card.Id, token), async () =>
        {
            // 唯一索引是最後一道防線；先查才能回固定的錯誤代碼並寫進稽核。
            if (await db.Set<Card>().AnyAsync(x => x.CardNumber == card.CardNumber, ct)) return AttendanceErrors.CardNumberDuplicate;
            db.Add(card);
            return Result.Success;
        }, ct);
        return outcome.IsSuccess ? CardDto.From(card, null) : outcome.Error;
    }
}
