using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Staff.Attendance.Cards;

/// <summary>登錄一張卡。<c>Kind</c> 是 <see cref="CardKinds"/> 之一；卡號去掉前後空白後必須唯一。</summary>
public sealed record CardCreateRequest(string CardNumber, string Kind, string? Note);

internal sealed class CardCreateRequestValidator : RequestValidator<CardCreateRequest>
{
    public CardCreateRequestValidator()
    {
        RuleFor(x => x.CardNumber).CardNumber();
        RuleFor(x => x.Kind).Kind();
        RuleFor(x => x.Note).Note();
    }
}

/// <summary>登錄卡片。會稽核（<c>admin.attendance_card</c>）。</summary>
internal sealed class CreateCard(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("", (CardCreateRequest body, ICurrentUser user, CreateCard handler, CancellationToken ct) => handler.HandleAsync(user.Id, body, ct).ToHttpResultAsync())
        .RequireAuthorization(Policies.AttendanceCardsMaintain)
        .WithName("CreateAttendanceCard");

    public async Task<Result<CardDto>> HandleAsync(Guid actor, CardCreateRequest request, CancellationToken ct)
    {
        var card = new Card { Id = Guid.NewGuid(), CardNumber = request.CardNumber.Trim(), Kind = request.Kind, Note = CardRules.Normalize(request.Note) };
        var outcome = await audit.MutateAsync(actor, FeatureCatalog.AttendanceCardsMaintain, "admin.attendance_card", card.Id, card.Id.ToString(), token => CardRules.SnapshotAsync(db, card.Id, token), async () =>
        {
            // 資料庫的唯一索引是最後一道防線；這裡先查，才能回固定的錯誤代碼並寫進稽核。
            if (await db.Set<Card>().AnyAsync(x => x.CardNumber == card.CardNumber, ct)) return AttendanceErrors.CardNumberDuplicate;
            db.Add(card);
            return Result.Success;
        }, ct);
        return outcome.IsSuccess ? CardDto.From(card, null) : outcome.Error!;
    }
}
