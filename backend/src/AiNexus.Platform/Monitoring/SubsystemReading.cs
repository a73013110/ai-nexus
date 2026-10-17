namespace AiNexus.Platform.Monitoring;

/// <summary><see cref="IMonitoringSource"/> 的讀數：狀態碼（見 <see cref="SubsystemStates"/>）、選填的單行摘要，以及指標清單。</summary>
public sealed record SubsystemReading(string Status, string? Summary, IReadOnlyList<SubsystemFact> Facts);

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
