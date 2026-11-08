using AiNexus.Data.App;
using AiNexus.Data.App.Releases;
using AiNexus.Platform.Scheduling;

namespace AiNexus.Features.Admin.Releases.Versions;

/// <summary>
/// 新版本第一次啟動時在部署歷史記一列（ADR 0028）。只記 <c>Release.ps1</c> 打包的程式：開發機每個 commit 都是不同版本，記了只是雜訊。
/// 每個程序啟動後跑一次，資料庫暫時連不上時每 5 分鐘重試到記上為止；IIS 重疊回收時新舊行程版本相同，第二個看到已記錄就略過。
/// </summary>
public sealed partial class AppDeploymentTask(AppDbContext db, AppRelease release, TimeProvider clock, ILogger<AppDeploymentTask> logger) : IScheduledTask
{
    public static ScheduledTaskDefinition Definition { get; } = new(
        "releases.record-deployment", "記錄部署", "版本", "新版本第一次啟動時在部署歷史記一列。", Schedule.AtStartup(TimeSpan.FromMinutes(5)))
    {
        Unavailable = services => services.GetRequiredService<AppRelease>().Packaged ? null : "只有發布包會記錄部署",
    };

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
        => await RecordAsync(ct) ? TaskOutcome.Done($"記錄 {release.Version}") : TaskOutcome.Idle($"{release.Version} 已記錄");

    /// <summary>寫入這一版的紀錄；已有時不重複寫，回傳 false。</summary>
    public async Task<bool> RecordAsync(CancellationToken ct)
    {
        var latest = await ReleaseCompatibility.LatestAsync(db, DeploymentComponents.App, ct);
        if (latest is not null && latest.Version == release.Version && latest.Revision == (release.Revision ?? "")) return false;
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
        return true;
    }

    [LoggerMessage(EventId = 5006, EventName = "releases.app_deployed", Level = LogLevel.Information, Message = "First start of this application version; deployment recorded.")]
    private static partial void LogRecorded(ILogger logger);
}
