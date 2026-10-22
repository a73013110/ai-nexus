namespace AiNexus.Features.Staff.Attendance.Imports;

/// <summary>打卡檔解析出的一筆刷卡；<see cref="PunchedAt"/> 固定 +08:00，<see cref="AttendanceDate"/> 是它的本地日期。</summary>
public sealed record ParsedPunch(string CardNumber, DateTimeOffset PunchedAt, DateOnly AttendanceDate, string MachineCode, string EventCode);
