namespace AiNexus.Platform.Dashboard;

/// <summary>
/// A generic overview section: headline metrics, daily series, a distribution, the viewer's own recent items and text
/// facts. Every list may be empty. <c>State</c> summarises the section for the hero: idle, active (work in progress)
/// or attention (failures to look at).
/// </summary>
public sealed record DashboardSectionDto(string Id, string Feature, string Title, string? Route, string State,
    IReadOnlyList<DashboardMetricDto> Metrics, IReadOnlyList<DashboardSeriesDto> Series, IReadOnlyList<DashboardShareDto> Shares,
    IReadOnlyList<DashboardItemDto> Items, IReadOnlyList<DashboardFactDto> Facts);

/// <summary>
/// One figure. <c>Value</c> is <c>null</c> when unknown, which is never shown as zero. <c>Format</c> is count, bytes,
/// percent or duration (milliseconds); <c>Period</c> marks figures that follow the reporting window.
/// </summary>
public sealed record DashboardMetricDto(string Key, string Label, double? Value, string Unit, string Format, string Tone, bool Period, string? Hint);

/// <summary>Daily values in the reporting window; days without data are omitted and read as zero.</summary>
public sealed record DashboardSeriesDto(string Key, string Label, string Unit, IReadOnlyList<DashboardPointDto> Points);

public sealed record DashboardPointDto(string Date, double Value);

/// <summary>One part of a distribution, such as documents by status.</summary>
public sealed record DashboardShareDto(string Key, string Label, double Value, string Tone);

/// <summary>A recent item of the viewer; <c>Link</c> is a workspace path.</summary>
public sealed record DashboardItemDto(string Id, string Title, string? Detail, DateTimeOffset? At, string? Link, string Tone);

/// <summary>A text fact, such as which retrieval mode is configured.</summary>
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
