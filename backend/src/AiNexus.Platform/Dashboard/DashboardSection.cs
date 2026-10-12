namespace AiNexus.Platform.Dashboard;

/// <summary>
/// Builds a <see cref="DashboardSectionDto"/> so sources read as a list of figures. The state becomes active or
/// attention from the tones used, unless set explicitly.
/// </summary>
public sealed class DashboardSection(string id, string feature, string title = "", string? route = null)
{
    private readonly List<DashboardMetricDto> metrics = [];
    private readonly List<DashboardSeriesDto> series = [];
    private readonly List<DashboardShareDto> shares = [];
    private readonly List<DashboardItemDto> items = [];
    private readonly List<DashboardFactDto> facts = [];
    private string? state;

    /// <summary>A current figure, such as how many collections exist now.</summary>
    public DashboardSection Count(string key, string label, double? value, string unit, string tone = DashboardTones.Neutral, string? hint = null)
        => Metric(new(key, label, value, unit, DashboardFormats.Count, tone, false, hint));

    /// <summary>A current size in bytes.</summary>
    public DashboardSection Size(string key, string label, long bytes, string? hint = null)
        => Metric(new(key, label, bytes, "", DashboardFormats.Bytes, DashboardTones.Neutral, false, hint));

    /// <summary>A figure that follows the reporting window, such as questions asked in the period.</summary>
    public DashboardSection Period(string key, string label, double? value, string unit, string format = DashboardFormats.Count,
        string tone = DashboardTones.Neutral, string? hint = null) => Metric(new(key, label, value, unit, format, tone, true, hint));

    public DashboardSection Metric(DashboardMetricDto metric) { metrics.Add(metric); return this; }

    public DashboardSection Daily(string key, string label, string unit, IReadOnlyList<DashboardPointDto> points)
    {
        series.Add(new(key, label, unit, points));
        return this;
    }

    /// <summary>A distribution part; zero parts are left out.</summary>
    public DashboardSection Share(string key, string label, double value, string tone = DashboardTones.Neutral)
    {
        if (value > 0) shares.Add(new(key, label, value, tone));
        return this;
    }

    public DashboardSection Item(string id, string title, string? detail, DateTimeOffset? at, string? link, string tone = DashboardTones.Neutral)
    {
        items.Add(new(id, title, detail, at, link, tone));
        return this;
    }

    public DashboardSection Fact(string label, string value, string tone = DashboardTones.Neutral)
    {
        facts.Add(new(label, value, tone));
        return this;
    }

    public DashboardSection State(string value) { state = value; return this; }

    /// <summary>Marks the section active when <paramref name="active"/> is above zero.</summary>
    public DashboardSection ActiveWhen(double? active) => active > 0 && state is null ? State(DashboardStates.Active) : this;

    public DashboardSectionDto Build() => new(id, feature, title, route, state ?? Derived(), metrics, series, shares, items, facts);

    private string Derived() => metrics.Any(x => x.Tone == DashboardTones.Danger && x.Value > 0) ? DashboardStates.Attention : DashboardStates.Idle;
}
