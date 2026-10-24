using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Office.Attendance.Cards;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Exclusions;

/// <summary>
/// 依卡號標為不列入出勤，已標過時只更新備註（PUT 冪等）。沒有帳號的資料（只有姓名的持有人、未歸戶刷卡）依附在卡號上，
/// 所以以卡號標記，同名同姓的人不會一起被隱藏；卡上綁了帳號的期間不受影響，使用者另以 <see cref="SaveRosterExclusion"/> 標記。
/// 卡號要已登錄為員工卡或出現在刷卡裡（未登錄的卡號也能標）；訪客卡本來就不列入。稽核與使用者共用 <c>admin.attendance_roster_exclusion</c>。
/// </summary>
internal sealed class SaveRosterCardExclusion(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/cards/{cardNumber}", (string cardNumber, RosterExclusionRequest body, ICurrentUser user, SaveRosterCardExclusion handler, CancellationToken ct)
            => handler.HandleAsync(user.Id, cardNumber, body, ct).ToHttpResultAsync())
        .RequireAuthorization(Policies.AttendanceCardsMaintain)
        .WithName("SaveAttendanceRosterCardExclusion");

    public async Task<Result> HandleAsync(Guid actor, string cardNumber, RosterExclusionRequest request, CancellationToken ct)
    {
        if (RosterCardExclusionSnapshot.Normalize(cardNumber) is not { } number) return AttendanceErrors.CardNotFound;
        return await audit.MutateAsync(actor, FeatureCatalog.AttendanceCardsMaintain, "admin.attendance_roster_exclusion", null, number,
            token => RosterCardExclusionSnapshot.ReadAsync(db, number, token), async () =>
            {
                var kind = await db.Set<Card>().Where(x => x.CardNumber == number).Select(x => x.Kind).SingleOrDefaultAsync(ct);
                if (kind == CardKinds.Visitor) return AttendanceErrors.CardIsVisitor;
                if (kind is null && !await db.Set<Punch>().AnyAsync(x => x.CardNumber == number, ct)) return AttendanceErrors.CardNotFound;
                var exclusion = await db.Set<RosterCardExclusion>().SingleOrDefaultAsync(x => x.CardNumber == number, ct);
                if (exclusion is null) db.Add(exclusion = new RosterCardExclusion { CardNumber = number });
                exclusion.Note = CardRules.Normalize(request.Note);
                return Result.Success;
            }, ct);
    }
}

/// <summary>依卡號標記的稽核快照與卡號正規化，新增、修改與移除共用：沒有標記時快照為 null。</summary>
internal static class RosterCardExclusionSnapshot
{
    /// <summary>去掉前後空白（與匯入、卡片登錄相同）；空白、過長或含控制字元的卡號不可能存在，視同找不到。</summary>
    public static string? Normalize(string? cardNumber)
        => cardNumber?.Trim() is { Length: > 0 and <= Card.CardNumberMaxLength } number && !number.Any(char.IsControl) ? number : null;

    public static async Task<object?> ReadAsync(AppDbContext db, string cardNumber, CancellationToken ct)
        => await db.Set<RosterCardExclusion>().AsNoTracking().AnyAsync(x => x.CardNumber == cardNumber, ct) ? new { cardNumber, excluded = true } : null;
}
