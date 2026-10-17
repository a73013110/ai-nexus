namespace AiNexus.Platform.Dashboard;

/// <summary>
/// 儀表板資料統計範圍與時間區間。<see cref="Owner"/> 為 <c>null</c> 時代表全平台範圍。
/// 當前狀態（如現有數量、活躍任務）不受時間區間限制；標記為期間指標（Period）的數值則統計於此區間內，並依檢視者之 UTC 偏移量進行曆日切齊。
/// 項目清單（最新成果等）則永遠屬於操作者 <see cref="Actor"/> 個人，不受統計範圍影響。
/// </summary>
public sealed record DashboardScope(Guid Actor, Guid? Owner, bool Platform, DateTimeOffset From, DateTimeOffset Until, int OffsetMinutes);
