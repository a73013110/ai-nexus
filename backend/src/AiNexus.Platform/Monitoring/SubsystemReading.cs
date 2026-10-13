namespace AiNexus.Platform.Monitoring;

/// <summary>What a <see cref="IMonitoringSource"/> reports: a status from <see cref="SubsystemStates"/>, an optional one-line summary and its figures.</summary>
public sealed record SubsystemReading(string Status, string? Summary, IReadOnlyList<SubsystemFact> Facts);

/// <summary>
/// One figure of a subsystem. <c>Value</c> is null when unknown; <c>Unit</c> (<see cref="SubsystemUnits"/>) tells the
/// page how to format it, and <c>text</c> facts carry their words in <c>Text</c> instead.
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
