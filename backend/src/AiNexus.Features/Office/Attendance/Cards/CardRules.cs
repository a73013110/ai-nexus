using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Cards;

/// <summary>卡片與持有期間共用的文字規則，以及卡片的稽核快照。</summary>
internal static class CardRules
{
    /// <summary>選填文字（備註、持有人姓名）：去掉前後空白後不超過資料表長度、不含控制字元。</summary>
    public static IRuleBuilderOptions<T, string?> OptionalText<T>(this IRuleBuilder<T, string?> rule, int maxLength)
        => rule.Must(x => x is null || x.Trim().Length <= maxLength).WithErrorCode("too_long")
            .Must(x => x is null || !x.Any(char.IsControl)).WithErrorCode("format");

    /// <summary>空白存成 null。</summary>
    public static string? Normalize(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>新增、修改與刪除卡片共用的稽核快照（<c>admin.attendance_card*</c>）；卡不存在時為 null。備註不進稽核，所以不讀。</summary>
    public static async Task<object?> SnapshotAsync(AppDbContext db, Guid id, CancellationToken ct)
        => await db.Set<Card>().AsNoTracking().Where(x => x.Id == id).Select(x => new { x.CardNumber, x.Kind }).SingleOrDefaultAsync(ct);
}
