namespace AiNexus.BuildingBlocks.Diagnostics;

public sealed class DiagnosticOptions
{
    public string Directory { get; set; } = "";
    public string ServiceName { get; set; } = "AiNexus";
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
    public LogLevel FrameworkMinimumLevel { get; set; } = LogLevel.Warning;
    public int QueueCapacity { get; set; } = 8192;
    public int ImportantQueueCapacity { get; set; } = 2048;
    public int BatchSize { get; set; } = 200;
    public int FlushIntervalMs { get; set; } = 1000;
    public int RetrySeconds { get; set; } = 10;
    public int SqlTimeoutSeconds { get; set; } = 5;
    public int ShutdownSeconds { get; set; } = 10;
    public long FileSizeBytes { get; set; } = 16 * 1024 * 1024;
    public long MaxDiskBytes { get; set; } = 2L * 1024 * 1024 * 1024;
    public long MaxSqlRows { get; set; } = 5_000_000;
    public int FileRetentionDays { get; set; } = 14;
    public int RetentionDays { get; set; } = 30;
    public int AuditRetentionDays { get; set; } = 365;
    public int CleanupBatchSize { get; set; } = 1000;
    public int LowLevelSampleEvery { get; set; } = 1;
    public int MaxQueryDays { get; set; } = 31;
    public int MaxExportRows { get; set; } = 5000;
    public int MaxExportDays { get; set; } = 7;
    public bool OtlpEnabled { get; set; }
    public string OtlpEndpoint { get; set; } = "http://localhost:4318";
    public bool Valid() => MinimumLevel is >= LogLevel.Trace and <= LogLevel.Critical && FrameworkMinimumLevel is >= LogLevel.Trace and <= LogLevel.Critical && ServiceName is { Length: > 0 and <= 80 }
        && QueueCapacity is >= 1 and <= 100000 && ImportantQueueCapacity is >= 1 and <= 20000
        && BatchSize is >= 1 and <= 1000 && FlushIntervalMs is >= 10 and <= 10000 && RetrySeconds is >= 1 and <= 300
        && SqlTimeoutSeconds is >= 1 and <= 30 && ShutdownSeconds is >= 1 and <= 30
        && FileSizeBytes is >= 65536 and <= 268435456 && MaxDiskBytes >= FileSizeBytes && MaxDiskBytes <= 1099511627776
        && MaxSqlRows is >= 1000 and <= 1_000_000_000 && FileRetentionDays is >= 1 and <= 365 && RetentionDays is >= 1 and <= 3650 && AuditRetentionDays is >= 30 and <= 36500
        && CleanupBatchSize is >= 1 and <= 10000 && LowLevelSampleEvery is >= 1 and <= 1000 && MaxQueryDays is >= 1 and <= 366
        && MaxExportRows is >= 1 and <= 10000 && MaxExportDays is >= 1 and <= 31
        && (!OtlpEnabled || Uri.TryCreate(OtlpEndpoint, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0);
}
