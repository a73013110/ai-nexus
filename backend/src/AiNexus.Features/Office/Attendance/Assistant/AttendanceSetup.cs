namespace AiNexus.Features.Office.Attendance.Assistant;

/// <summary>
/// 一個人在出勤名單上的設定：決定他會不會出現在出勤紀錄、出現在哪些日子（<see cref="Records.AttendanceRoster.Rules"/>）。
/// <paramref name="Active"/> 為 null 是還沒綁帳號、只有姓名的持有人；false 是帳號停用或刪除（ADR 0019，停用＝離職）。
/// </summary>
public sealed record AttendanceSetup(Guid? UserId, string DisplayName, string? Account, bool? Active, IReadOnlyList<AttendanceSetupHolding> Holdings);

/// <summary>一段持有期間 [<paramref name="StartsAt"/>, <paramref name="EndsAt"/>)，連同卡號與那張卡是否在出勤紀錄隱藏。</summary>
public sealed record AttendanceSetupHolding(string CardNumber, bool Hidden, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string? Note);
