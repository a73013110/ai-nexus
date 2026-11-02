using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Cards;

/// <summary>新增與修改持有期間的請求共有的欄位；只為了讓兩者共用 <see cref="AssignmentRules.AddFieldRules"/>。</summary>
internal interface IAssignmentFields
{
    Guid? UserId { get; }
    string? HolderName { get; }
    DateTimeOffset StartsAt { get; }
    DateTimeOffset? EndsAt { get; }
    string? Note { get; }
}

/// <summary>
/// 持有期間的規則：[StartsAt, EndsAt) 不可與同一張卡的其他期間重疊（資料庫不擋，程式保證），指派已有人持有的卡必須明確接手。
/// 需要資料的檢查在 <see cref="AdministrativeWrite"/> 的交易與這張卡的鎖內執行；<see cref="Place"/> 只看已讀出的期間，不碰資料庫。
/// </summary>
internal static class AssignmentRules
{
    private static readonly RuleCode Period = new("period");
    private static readonly RuleCode HolderRequired = new("holder_required");

    /// <summary>格式規則與資料表的 check（持有人至少一個、結束晚於開始）一致，才能在碰資料庫前回固定的代碼。</summary>
    public static void AddFieldRules<T>(this AbstractValidator<T> validator) where T : IAssignmentFields
    {
        validator.RuleFor(x => x.StartsAt).Must(x => x != default).WithErrorCode(RuleCode.Required);
        validator.RuleFor(x => x.EndsAt).Must((request, end) => end is null || end > request.StartsAt).WithErrorCode(Period);
        validator.RuleFor(x => x).Must(x => x.UserId is not null || !string.IsNullOrWhiteSpace(x.HolderName)).WithErrorCode(HolderRequired).OverridePropertyName("holder");
        validator.RuleFor(x => x.HolderName).OptionalText(CardAssignment.HolderNameMaxLength);
        validator.RuleFor(x => x.Note).OptionalText(CardAssignment.NoteMaxLength);
    }

    /// <summary>持有人只能是未刪除的使用者。</summary>
    public static Task<bool> HolderExistsAsync(AppDbContext db, Guid userId, CancellationToken ct)
        => db.Users.AnyAsync(x => x.Id == userId && x.DeletedAt == null, ct);

    /// <summary>兩段 [開始, 結束) 是否重疊；結束為 null 表示持有中。首尾相接（前一段結束＝後一段開始）不算重疊。</summary>
    public static bool Overlaps(DateTimeOffset startsAt, DateTimeOffset? endsAt, CardAssignment other)
        => startsAt < (other.EndsAt ?? DateTimeOffset.MaxValue) && other.StartsAt < (endsAt ?? DateTimeOffset.MaxValue);

    /// <summary>
    /// 把 <paramref name="added"/> 放進已有 <paramref name="existing"/> 這些期間的卡；拒絕時回傳錯誤，接手時直接改掉前一段的結束時間。
    /// <list type="bullet">
    /// <item>不重疊：直接新增。</item>
    /// <item>任何重疊的既有期間與新段同時或更晚開始：一律拒絕（<see cref="AttendanceErrors.StartsNotAfterPrevious"/>），接手只會結束前一段的尾端，不能往前蓋。</item>
    /// <item>否則重疊的只會是涵蓋新段開始時間的那一段。新段比它早結束時拒絕（<see cref="AttendanceErrors.AssignmentOverlap"/>）：
    /// 接手會把它結束在新段開始，它在新段結束之後的那一截就悄悄變成無人持有。其餘沒有明確接手回 <see cref="AttendanceErrors.CardHeld"/>。</item>
    /// <item><paramref name="takeOverRowVersion"/> 是使用者確認接手時看到的那一段的版本：確認之後那一段又被改過（可能已換人）回 <see cref="AccessControlErrors.WriteConflict"/>，不結束使用者沒看過的期間。</item>
    /// </list>
    /// </summary>
    public static Error? Place(IReadOnlyCollection<CardAssignment> existing, CardAssignment added, long? takeOverRowVersion)
    {
        var overlapping = existing.Where(x => Overlaps(added.StartsAt, added.EndsAt, x)).ToList();
        if (overlapping.Count == 0) return null;
        if (overlapping.Any(x => x.StartsAt >= added.StartsAt)) return AttendanceErrors.StartsNotAfterPrevious;
        // 壞資料（既有期間彼此重疊）時不猜，交給人處理。
        if (overlapping.Count > 1) return AttendanceErrors.AssignmentOverlap;
        var holder = overlapping[0];
        if (added.EndsAt is { } end && (holder.EndsAt is null || end < holder.EndsAt)) return AttendanceErrors.AssignmentOverlap;
        if (takeOverRowVersion is null) return AttendanceErrors.CardHeld;
        if (takeOverRowVersion != holder.RowVersion) return AccessControlErrors.WriteConflict;
        holder.EndsAt = added.StartsAt;
        return null;
    }

    /// <summary>
    /// 持有期間行政寫入共用的對象：稽核的資源是這段期間，快照則是 <paramref name="cardId"/> 整張卡（見 <see cref="SnapshotAsync"/>）。
    /// 同一張卡的寫入排隊，重疊的檢查才不會雙雙通過；不知道是哪張卡（期間不存在）時沒有東西要保護，也就不取鎖。
    /// </summary>
    public static AuditTarget Target(AppDbContext db, Guid id, Guid? cardId)
    {
        var target = AuditTarget.Of(id, ct => SnapshotAsync(db, cardId, ct));
        return cardId is { } card ? target.Locking(AttendanceLocks.Card(card)) : target;
    }

    /// <summary>持有期間所屬的卡；修改與刪除的稽核快照以整張卡為單位，刪除後就查不到，所以要在變更前先查。</summary>
    public static Task<Guid?> CardOfAsync(AppDbContext db, Guid assignmentId, CancellationToken ct)
        => db.CardAssignments.AsNoTracking().Where(x => x.Id == assignmentId).Select(x => (Guid?)x.CardId).SingleOrDefaultAsync(ct);

    /// <summary>
    /// 一張卡的全部持有期間，給稽核快照（<c>admin.attendance_assignment*</c>）：接手會同時改掉前一段的結束時間，只看單一列會漏掉。
    /// 卡不存在（或不知道是哪張卡）時為 null。備註不進稽核（<c>AuditRedactor</c> 不收自由文字），所以不讀。
    /// </summary>
    private static async Task<object?> SnapshotAsync(AppDbContext db, Guid? cardId, CancellationToken ct)
    {
        if (cardId is not { } id) return null;
        var number = await db.Cards.AsNoTracking().Where(x => x.Id == id).Select(x => x.CardNumber).SingleOrDefaultAsync(ct);
        if (number is null) return null;
        return new
        {
            cardId = id,
            cardNumber = number,
            assignments = await db.CardAssignments.AsNoTracking().Where(x => x.CardId == id).OrderBy(x => x.StartsAt).ThenBy(x => x.Id)
                .Select(x => new { x.Id, x.UserId, x.HolderName, x.StartsAt, x.EndsAt }).ToArrayAsync(ct),
        };
    }
}
