namespace AiNexus.Platform.Dashboard;

/// <summary>
/// 儀表板資料統計範圍與時間區間。<see cref="Owner"/> 為 <c>null</c> 時代表全平台範圍。
/// 目前狀態（如現有數量、進行中的任務）不受時間區間限制；Period 指標則統計於區間內，
/// 並依檢視者的 UTC 偏移量切齊曆日。項目清單（如最新成果）永遠屬於 <see cref="Actor"/> 本人，不受範圍影響。
/// </summary>
public sealed record DashboardScope(Guid Actor, Guid? Owner, bool Platform, DateTimeOffset From, DateTimeOffset Until, int OffsetMinutes);
