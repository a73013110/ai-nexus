namespace AiNexus.Platform.Dashboard;

/// <summary>
/// 儀表板通用總覽區塊 DTO：包含重點指標、每日序列、分佈比例、檢視者個人的近期項目以及文字事實。
/// 各清單皆允許為空。<c>State</c> 彙總該區塊的焦點狀態：idle（閒置）、active（進行中）或 attention（待關注之異常）。
/// </summary>
public sealed record DashboardSectionDto(string Id, string Feature, string Title, string? Route, string State,
    IReadOnlyList<DashboardMetricDto> Metrics, IReadOnlyList<DashboardSeriesDto> Series, IReadOnlyList<DashboardShareDto> Shares,
    IReadOnlyList<DashboardItemDto> Items, IReadOnlyList<DashboardFactDto> Facts);

/// <summary>
/// 單一指標數據。<c>Value</c> 為 <c>null</c> 代表未知，畫面上絕不顯示為零。<c>Format</c> 代表數量、位元組、百分比或時間長度（毫秒）；<c>Period</c> 標記此數值是否隨時間統計區間變動。
/// </summary>
public sealed record DashboardMetricDto(string Key, string Label, double? Value, string Unit, string Format, string Tone, bool Period, string? Hint);

/// <summary>統計區間內的每日時間序列數據；無資料的曆日自動省略並視為 0。</summary>
public sealed record DashboardSeriesDto(string Key, string Label, string Unit, IReadOnlyList<DashboardPointDto> Points);

public sealed record DashboardPointDto(string Date, double Value);

/// <summary>分佈佔比項目，例如依文件狀態區分的份額。</summary>
public sealed record DashboardShareDto(string Key, string Label, double Value, string Tone);

/// <summary>檢視者個人近期的資源項目；<c>Link</c> 為工作區內部路徑。</summary>
public sealed record DashboardItemDto(string Id, string Title, string? Detail, DateTimeOffset? At, string? Link, string Tone);

/// <summary>文字事實說明，例如當前啟用的檢索模式。</summary>
public sealed record DashboardFactDto(string Label, string Value, string Tone);

public static class DashboardTones
{
    public const string Neutral = "neutral", Info = "info", Success = "success", Warning = "warning", Danger = "danger";
}

public static class DashboardStates
{
    public const string Idle = "idle", Active = "active", Attention = "attention";
}

public static class DashboardFormats
{
    public const string Count = "count", Bytes = "bytes", Percent = "percent", Duration = "duration";
}
