namespace AiNexus.Features.Staff.Attendance.Imports;

/// <summary>打卡檔內容不合格式；訊息是給 HR 看的繁體中文（不含檔案內容），會原樣存進匯入紀錄的 <c>ErrorMessage</c>。</summary>
public sealed class PunchFileFormatException(string message) : Exception(message);
