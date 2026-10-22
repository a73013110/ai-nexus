using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Staff.Attendance.Cards;

/// <summary>卡片的格式規則與稽核快照；新增、修改、刪除共用。</summary>
internal static class CardRules
{
    public const int NoteMaxLength = 200;

    /// <summary>
    /// 卡號是打卡檔裡的原始字串（不補零、不轉數字），只去掉前後空白後存入，之後與刷卡以相同字串比對。
    /// 長度上限同資料表；控制字元會讓打卡檔的逐行解析與畫面顯示出錯，所以擋掉。
    /// </summary>
    public static IRuleBuilderOptions<T, string> CardNumber<T>(this IRuleBuilder<T, string> rule)
        => rule.Must(x => !string.IsNullOrWhiteSpace(x)).WithErrorCode("required")
            .Must(x => x is null || x.Trim().Length <= 32).WithErrorCode("too_long")
            .Must(x => x is null || !x.Any(char.IsControl)).WithErrorCode("format");

    public static IRuleBuilderOptions<T, string> Kind<T>(this IRuleBuilder<T, string> rule)
        => rule.Must(x => x is CardKinds.Employee or CardKinds.Visitor).WithErrorCode("unknown");

    public static IRuleBuilderOptions<T, string?> Note<T>(this IRuleBuilder<T, string?> rule)
        => rule.Must(x => x is null || x.Trim().Length <= NoteMaxLength).WithErrorCode("too_long")
            .Must(x => x is null || !x.Any(char.IsControl)).WithErrorCode("format");

    /// <summary>空白備註存成 null。</summary>
    public static string? Normalize(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>新增、修改與刪除卡片共用的稽核快照（<c>admin.attendance_card*</c>）；卡不存在時為 null。</summary>
    public static async Task<object?> SnapshotAsync(AppDbContext db, Guid id, CancellationToken ct)
        => await db.Set<Card>().AsNoTracking().Where(x => x.Id == id).Select(x => new { x.CardNumber, x.Kind, x.Note }).SingleOrDefaultAsync(ct);
}
