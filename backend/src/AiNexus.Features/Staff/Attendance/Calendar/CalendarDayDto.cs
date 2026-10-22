namespace AiNexus.Features.Staff.Attendance.Calendar;

/// <summary>GD 假日行事曆（<c>dbo.Pub_Days</c>）的一天：<c>MorningOff</c>／<c>AfternoonOff</c> 為 true＝該半天放假（GD 的 <c>AM</c>、<c>PM</c>），<c>Remark</c> 是 <c>hReMark</c>。</summary>
public sealed record CalendarDayDto(DateOnly Date, bool MorningOff, bool AfternoonOff, string? Remark);
