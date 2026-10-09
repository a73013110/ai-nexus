namespace AiNexus.Features.Monitoring;

/// <summary>Suppress observer-generated traffic across middleware, SQL and outgoing HTTP.</summary>
public sealed class MonitoringSuppression : IDisposable
{
    private static readonly AsyncLocal<int> Depth = new();
    public static bool Active => Depth.Value > 0;
    public MonitoringSuppression() => Depth.Value++;
    public void Dispose() => Depth.Value--;
}
