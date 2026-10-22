using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Platform.Errors;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Staff.Attendance.Cards;

/// <summary>
/// 持有期間的規則：[StartsAt, EndsAt) 不可與同一張卡的其他期間重疊（資料庫不擋，程式保證），指派已有人持有的卡必須明確接手。
/// 需要資料的檢查在 <c>AdministrativeAudit</c> 的交易內執行；這裡的 <see cref="Place"/> 只看已讀出的期間，不碰資料庫。
/// </summary>
internal static class AssignmentRules
{
    public const int HolderNameMaxLength = 120;

    /// <summary>新增與修改共用的格式規則；與資料表的 check（持有人至少一個、結束晚於開始）一致，才能在碰資料庫前回固定的代碼。</summary>
    public static void AddFieldRules<T>(this AbstractValidator<T> validator) where T : IAssignmentFields
    {
        validator.RuleFor(x => x.StartsAt).Must(x => x != default).WithErrorCode("required");
        validator.RuleFor(x => x.EndsAt).Must((request, end) => end is null || end > request.StartsAt).WithErrorCode("period");
        validator.RuleFor(x => x).Must(x => x.UserId is not null || !string.IsNullOrWhiteSpace(x.HolderName)).WithErrorCode("holder_required").OverridePropertyName("holder");
        validator.RuleFor(x => x.HolderName)
            .Must(x => x is null || x.Trim().Length <= HolderNameMaxLength).WithErrorCode("too_long")
            .Must(x => x is null || !x.Any(char.IsControl)).WithErrorCode("format");
        validator.RuleFor(x => x.Note).Note();
    }

    /// <summary>兩段 [開始, 結束) 是否重疊；結束為 null 表示持有中（無限遠）。首尾相接（前一段結束＝後一段開始）不算重疊。</summary>
    public static bool Overlaps(DateTimeOffset startsAt, DateTimeOffset? endsAt, CardAssignment other)
        => startsAt < (other.EndsAt ?? DateTimeOffset.MaxValue) && other.StartsAt < (endsAt ?? DateTimeOffset.MaxValue);

    /// <summary>新段的安置結果：<see cref="Error"/> 不為 null 就是拒絕；<see cref="EndsPrevious"/> 是接手時要把結束時間改成新段開始的那一段。</summary>
    public readonly record struct Placement(Error? Error, CardAssignment? EndsPrevious);

    /// <summary>
    /// 新增一段持有期間 [<paramref name="startsAt"/>, <paramref name="endsAt"/>) 到有 <paramref name="existing"/> 這些期間的卡上。
    /// <list type="bullet">
    /// <item>不重疊：直接新增。</item>
    /// <item>與任何重疊的既有期間同一時刻或更早開始：一律拒絕（<see cref="AttendanceErrors.StartsNotAfterPrevious"/>），接手只會結束前一段的尾端，不能往前蓋。</item>
    /// <item>否則重疊的只會是涵蓋新段開始時間的那一段（之前沒有任何人與它重疊）：新段若會把它切成兩段（新段結束在它中間）拒絕；
    /// 沒有明確接手回 <see cref="AttendanceErrors.CardHeld"/>，有就回傳要結束的那一段。</item>
    /// </list>
    /// </summary>
    public static Placement Place(IReadOnlyCollection<CardAssignment> existing, DateTimeOffset startsAt, DateTimeOffset? endsAt, bool takeOver)
    {
        var overlapping = existing.Where(x => Overlaps(startsAt, endsAt, x)).ToList();
        if (overlapping.Count == 0) return default;
        if (overlapping.Any(x => x.StartsAt >= startsAt)) return new(AttendanceErrors.StartsNotAfterPrevious, null);
        // 壞資料（既有期間彼此重疊）時不猜，交給人處理。
        if (overlapping.Count > 1) return new(AttendanceErrors.AssignmentOverlap, null);
        var holder = overlapping[0];
        var tailCovered = holder.EndsAt is null ? endsAt is null : endsAt is null || endsAt >= holder.EndsAt;
        if (!tailCovered) return new(AttendanceErrors.AssignmentOverlap, null);
        return takeOver ? new(null, holder) : new(AttendanceErrors.CardHeld, null);
    }

    /// <summary>
    /// 一張卡的全部持有期間，給稽核快照（<c>admin.attendance_assignment*</c>）：接手會同時改掉前一段的結束時間，只看單一列會漏掉。
    /// 卡不存在（或不知道是哪張卡）時為 null。
    /// </summary>
    public static async Task<object?> SnapshotAsync(AppDbContext db, Guid? cardId, CancellationToken ct)
    {
        if (cardId is not { } id) return null;
        var number = await db.Set<Card>().AsNoTracking().Where(x => x.Id == id).Select(x => x.CardNumber).SingleOrDefaultAsync(ct);
        if (number is null) return null;
        return new
        {
            cardId = id,
            cardNumber = number,
            assignments = await db.Set<CardAssignment>().AsNoTracking().Where(x => x.CardId == id).OrderBy(x => x.StartsAt).ThenBy(x => x.Id)
                .Select(x => new { x.Id, x.UserId, x.HolderName, x.StartsAt, x.EndsAt, x.Note }).ToArrayAsync(ct),
        };
    }
}
