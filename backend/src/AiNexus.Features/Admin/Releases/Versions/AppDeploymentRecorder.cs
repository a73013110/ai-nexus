using AiNexus.Data.App;
using AiNexus.Data.App.Releases;

namespace AiNexus.Features.Admin.Releases.Versions;

/// <summary>
/// 新版本第一次啟動時在部署歷史記一列（ADR 0028）。只記 <c>Release.ps1</c> 打包的程式：開發機每個 commit 都是不同版本，記了只是雜訊。
/// 資料庫暫時連不上時每 <see cref="RetryInterval"/> 重試，記到為止；IIS 重疊回收時新舊行程版本相同，第二個看到已記錄就略過。
/// </summary>
public sealed partial class AppDeploymentRecorder(
    IServiceScopeFactory scopes,
    StorageReadiness readiness,
    AppRelease release,
    TimeProvider clock,
    ILogger<AppDeploymentRecorder> logger) : BackgroundService
{
    public static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!release.Packaged) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (readiness.Configured && await RecordAsync(stoppingToken) is not null) return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogUnrecorded(logger, ex.GetType().Name);
            }
            await Task.Delay(RetryInterval, clock, stoppingToken);
        }
    }

    /// <summary>寫入或確認已有這一版的紀錄，回傳那一列。</summary>
    public async Task<Deployment?> RecordAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var latest = await ReleaseCompatibility.LatestAsync(db, DeploymentComponents.App, ct);
        if (latest is not null && latest.Version == release.Version && latest.Revision == (release.Revision ?? "")) return latest;
        var deployment = new Deployment
        {
            Component = DeploymentComponents.App,
            Version = release.Version,
            Revision = release.Revision ?? "",
            DeployedAt = clock.GetUtcNow(),
            Host = Environment.MachineName,
        };
        db.Add(deployment);
        await db.SaveChangesAsync(ct);
        LogRecorded(logger);
        return deployment;
    }

    [LoggerMessage(EventId = 5006, EventName = "releases.app_deployed", Level = LogLevel.Information, Message = "First start of this application version; deployment recorded.")]
    private static partial void LogRecorded(ILogger logger);

    [LoggerMessage(EventId = 5007, EventName = "releases.app_deployment_unrecorded", Level = LogLevel.Warning,
        Message = "Application deployment could not be recorded ({ErrorType}); retrying later.")]
    private static partial void LogUnrecorded(ILogger logger, string errorType);
}
