namespace AiNexus.Platform.Monitoring;

/// <summary>
/// <see cref="IMonitoringSource"/> 的讀數：狀態碼（見 <see cref="SubsystemStates"/>）、選填的單行摘要、指標清單，
/// 以及停站時手上工作的下場（<see cref="StopImpact"/>；不持有工作的來源省略）。
/// </summary>
public sealed record SubsystemReading(string Status, string? Summary, IReadOnlyList<SubsystemFact> Facts, StopImpact? Stop = null);

/// <summary>
/// 現在停站（發版、回收集區）時這個子系統手上尚未完成的工作：<c>Interrupts</c> 只存在程序內、停站即失敗，要使用者重來；
/// <c>Resumes</c> 已存在資料庫，重啟後自動接續，只是晚一點完成。監控頁加總成「現在能不能停站」。
/// </summary>
public sealed record StopImpact(int Interrupts, int Resumes);

/// <summary>
/// 子系統的單一指標。<c>Value</c> 為 null 代表未知；<c>Unit</c>（見 <see cref="SubsystemUnits"/>）決定前端如何格式化；
/// 文字型指標的內容放在 <c>Text</c>。
/// </summary>
public sealed record SubsystemFact(string Id, string Label, double? Value, string Unit, string? Text, string Tone)
{
    public static SubsystemFact Count(string id, string label, double? value, string tone = SubsystemTones.Neutral) => new(id, label, value, SubsystemUnits.Count, null, tone);
    public static SubsystemFact Bytes(string id, string label, double? value, string tone = SubsystemTones.Neutral) => new(id, label, value, SubsystemUnits.Bytes, null, tone);
    public static SubsystemFact Percent(string id, string label, double? value, string tone = SubsystemTones.Neutral) => new(id, label, value, SubsystemUnits.Percent, null, tone);
    public static SubsystemFact Seconds(string id, string label, double? value, string tone = SubsystemTones.Neutral) => new(id, label, value, SubsystemUnits.Seconds, null, tone);
    public static SubsystemFact Tokens(string id, string label, double? value, string tone = SubsystemTones.Neutral) => new(id, label, value, SubsystemUnits.Tokens, null, tone);
    public static SubsystemFact Words(string id, string label, string text, string tone = SubsystemTones.Neutral) => new(id, label, null, SubsystemUnits.Text, text, tone);
}

public static class SubsystemStates
{
    public const string Healthy = "healthy", Degraded = "degraded", Failing = "failing", Idle = "idle", Unobserved = "unobserved";
}

public static class SubsystemUnits
{
    public const string Count = "count", Bytes = "bytes", Percent = "percent", Milliseconds = "ms", Seconds = "seconds", Tokens = "tokens", Text = "text";
}

public static class SubsystemTones
{
    public const string Neutral = "neutral", Positive = "positive", Warning = "warning", Danger = "danger";
}
