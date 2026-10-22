using System.Globalization;
using System.Text.Json;
using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Staff.Attendance.Cards;
using AiNexus.Features.Staff.Attendance.Gd;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Staff.Attendance.Calendar;

/// <summary>某一天要設成的值；<c>Remark</c> 去掉前後空白，空白存成 null。</summary>
public sealed record CalendarDayRequest(bool MorningOff, bool AfternoonOff, string? Remark);

/// <summary>
/// 這裡只擋字元數與控制字元；GD 的 <c>hReMark</c> 是 varchar(255)，實際上限是「位元組」且取決於 GD 的定序，
/// 由 <see cref="GdCalendarWriter"/> 寫入後讀回比對（<c>attendance_calendar_remark_invalid</c>）。
/// </summary>
internal sealed class CalendarDayRequestValidator : RequestValidator<CalendarDayRequest>
{
    public const int RemarkMaxLength = 255;

    public CalendarDayRequestValidator()
    {
        RuleFor(x => x.Remark).OptionalText(RemarkMaxLength);
    }
}

/// <summary>
/// HR 修改 GD 某一天的上午／下午放假與備註（ADR 0018 允許的例外回寫），回傳修改後的值。
/// 修改者寫操作者的公文帳號（<c>Sys_User.ID</c>，以 AD 帳號換算），對不到唯一一個就拒絕，不退回寫 <c>sys</c>。
/// 每次嘗試都稽核（<c>admin.calendar_day</c>），成功含修改前後值，失敗含錯誤代碼。
/// </summary>
/// <remarks>
/// GD 與本系統是兩個資料庫，無法同一個交易。順序是：鎖定 GD 的這一天並讀前值 → 更新（未提交）→ 在本系統寫 saved 稽核 → 提交 GD。
/// <list type="bullet">
/// <item>稽核寫入失敗（App 資料庫有問題）：GD 交易回滾，什麼都沒改，請求以 503 結束。不會發生「GD 改了卻沒有稽核」。</item>
/// <item>GD 提交失敗：已有一筆 saved 稽核，所以再補一筆 <c>attendance_gd_unavailable</c> 的失敗紀錄並回 503；
/// 網路在提交途中中斷時 GD 可能其實已提交，以兩筆紀錄並存表示「結果不確定」，HR 重新讀取即可確認。</item>
/// </list>
/// 鎖只持有到稽核寫完（一次本機 SQL 寫入），公文系統同時讀這一天時最多等這麼久。
/// </remarks>
internal sealed class UpdateCalendarDay(AppDbContext db, GdAccounts accounts, GdCalendarWriter writer)
{
    public const string AuditAction = "admin.calendar_day";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/{date}", (DateOnly date, CalendarDayRequest body, ICurrentUser user, UpdateCalendarDay handler, CancellationToken ct) => handler.HandleAsync(user.Id, date, body, ct).ToHttpResultAsync())
        .RequireAuthorization(Policies.AttendanceCalendarMaintain)
        .WithName("UpdateAttendanceCalendarDay");

    public async Task<Result<CalendarDayDto>> HandleAsync(Guid actor, DateOnly date, CalendarDayRequest request, CancellationToken ct)
    {
        var key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var after = new GdCalendarValues(request.MorningOff, request.AfternoonOff, CardRules.Normalize(request.Remark));

        // 先檢查不必連 GD 的條件；未設定或設為唯讀是部署問題，不必等到查帳號才發現。
        if (writer.CanWrite().Error is { } refused) return await RefuseAsync(actor, key, refused);
        if (!CalendarDates.IsSupported(date)) return await RefuseAsync(actor, key, AttendanceErrors.CalendarDayNotFound);
        var map = await accounts.ResolveAsync([actor], ct);
        if (map is null) return await RefuseAsync(actor, key, AttendanceErrors.GdUnavailable);
        // 同一個 AD 帳號對到多筆 Sys_User 時不猜：寫錯修改者比拒絕更難察覺。
        var gdIds = map.GdIds.GetValueOrDefault(actor)?.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (gdIds is not [var gdUserId] || gdUserId.Length > GdCalendarWriter.UserIdMaxLength) return await RefuseAsync(actor, key, AttendanceErrors.GdAccountUnmapped);

        var opened = await writer.UpdateAsync(date, after, gdUserId, ct);
        if (!opened.IsSuccess) return await RefuseAsync(actor, key, opened.Error);
        await using var edit = opened.Value;
        var before = Snapshot(edit.Before, edit.PreviousUserId);

        // GD 已更新、尚未提交：之後不再接受取消，免得稽核寫到一半或寫完才放棄，讓兩邊不一致。
        Record(actor, "saved", new { resourceKey = key, before, after = Snapshot(after, gdUserId) });
        await db.SaveChangesAsync(CancellationToken.None);
        if (!await edit.CommitAsync()) return await RefuseAsync(actor, key, AttendanceErrors.GdUnavailable, before);
        return new CalendarDayDto(date, after.MorningOff, after.AfternoonOff, after.Remark);
    }

    // 欄位名稱必須在 AuditRedactor 的白名單內（calendarRemark、gdUserId 等），JSON 形狀即稽核合約。
    private static object Snapshot(GdCalendarValues values, string? gdUserId)
        => new { values.MorningOff, values.AfternoonOff, calendarRemark = values.Remark, gdUserId };

    private async Task<Error> RefuseAsync(Guid actor, string key, Error error, object? before = null)
    {
        Record(actor, error.Code, new { resourceKey = key, before, failureCode = error.Code });
        await db.SaveChangesAsync(CancellationToken.None);
        return error;
    }

    private void Record(Guid actor, string result, object details) => db.AuditEvents.Add(new()
    {
        OwnerId = actor,
        Action = AuditAction,
        Result = result,
        DetailsJson = JsonSerializer.Serialize(details, Json),
    });
}
