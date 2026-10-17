namespace AiNexus.Platform.Dashboard;

/// <summary>
/// 儀表板區塊建立器。將各模組指標資料組裝為 <see cref="DashboardSectionDto"/>。
/// 除非明確指定，否則區塊狀態會自動根據指標色調（Tone）推導為 active 或 attention。
/// </summary>
public sealed class DashboardSection(string id, string feature, string title = "", string? route = null)
{
    private readonly List<DashboardMetricDto> metrics = [];
    private readonly List<DashboardSeriesDto> series = [];
    private readonly List<DashboardShareDto> shares = [];
    private readonly List<DashboardItemDto> items = [];
    private string? state;

    /// <summary>當前狀態指標，例如現存的知識庫集合總數。</summary>
    public DashboardSection Count(string key, string label, double? value, string unit, string tone = DashboardTones.Neutral, string? hint = null)
        => Metric(new(key, label, value, unit, DashboardFormats.Count, tone, false, hint));

    /// <summary>當前容量大小指標（以位元組為單位）。</summary>
    public DashboardSection Size(string key, string label, long bytes, string? hint = null)
        => Metric(new(key, label, bytes, "", DashboardFormats.Bytes, DashboardTones.Neutral, false, hint));

    /// <summary>依統計時間區間累計的指標，例如該期間內的提問總次數。</summary>
    public DashboardSection Period(string key, string label, double? value, string unit, string format = DashboardFormats.Count,
        string tone = DashboardTones.Neutral, string? hint = null) => Metric(new(key, label, value, unit, format, tone, true, hint));

    public DashboardSection Metric(DashboardMetricDto metric) { metrics.Add(metric); return this; }

    public DashboardSection Daily(string key, string label, string unit, IReadOnlyList<DashboardPointDto> points)
    {
        series.Add(new(key, label, unit, points));
        return this;
    }

    /// <summary>分佈佔比項目；數值為 0 者自動略過。</summary>
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

    public DashboardSection State(string value) { state = value; return this; }

    /// <summary>當 <paramref name="active"/> 大於零時，將此區塊標記為活躍狀態（Active）。</summary>
    public DashboardSection ActiveWhen(double? active) => active > 0 && state is null ? State(DashboardStates.Active) : this;

    /// <summary>建立區塊 DTO。注意 Facts 保持為空，因系統配置與服務狀態屬於維運管理頁面範疇。</summary>
    public DashboardSectionDto Build() => new(id, feature, title, route, state ?? Derived(), metrics, series, shares, items, []);

    private string Derived() => metrics.Any(x => x.Tone == DashboardTones.Danger && x.Value > 0) ? DashboardStates.Attention : DashboardStates.Idle;
}
