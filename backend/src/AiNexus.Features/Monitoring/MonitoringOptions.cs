namespace AiNexus.Features.Monitoring;

public sealed class MonitoringOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxSessions { get; set; } = 2000;
    public int SessionTimeoutSeconds { get; set; } = 90;
    public int RefreshSeconds { get; set; } = 3;
    public bool Valid() => MaxSessions is >= 100 and <= 10000 && SessionTimeoutSeconds is >= 60 and <= 300 && RefreshSeconds is >= 2 and <= 15;
}
