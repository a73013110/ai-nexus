namespace AiNexus.Features.Monitoring;

/// <summary>在中介層、SQL 與外送 HTTP 之間，抑制觀測工具自身產生的流量。</summary>
public sealed class MonitoringSuppression : IDisposable
{
    // AsyncLocal 讓抑制範圍跟著非同步呼叫流動；用計數而非布林，才能安全巢狀使用。
    private static readonly AsyncLocal<int> Depth = new();
    public static bool Active => Depth.Value > 0;
    public MonitoringSuppression() => Depth.Value++;
    public void Dispose() => Depth.Value--;
}
