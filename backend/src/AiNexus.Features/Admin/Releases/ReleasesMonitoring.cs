using AiNexus.Data.App;
using AiNexus.Data.App.Releases;
using AiNexus.Features.Admin.Releases.SchemaChanges;
using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Admin.Releases;

/// <summary>
/// 「資料庫結構」卡（ADR 0028）：資料庫版本低於程式需要的最低版本 failing；有異動 degraded；一致 healthy；沒有快照（從未執行 update.sql 或建庫腳本）idle。
/// 異動計算要讀整個資料庫的 metadata，所以快取 10 分鐘，只傳回有差異的列。
/// </summary>
internal sealed class ReleasesMonitoring(AppDbContext db, AppRelease release, SchemaChangeReader reader) : IMonitoringSource
{
    public string Id => "database-schema";
    public string Name => "資料庫結構";
    public int Order => 90;
    public TimeSpan Refresh => TimeSpan.FromMinutes(10);

    public async Task<SubsystemReading> ReadAsync(CancellationToken ct)
    {
        var database = await ReleaseCompatibility.LatestAsync(db, DeploymentComponents.Database, ct);
        var compatibility = ReleaseCompatibility.Of(release.MinimumDatabaseVersion, database?.Version);
        var snapshot = await reader.HasSnapshotAsync(ct);
        var changes = snapshot ? await reader.ListAsync(ct) : [];
        var (status, summary) =
            compatibility == ReleaseCompatibility.Incompatible ? (SubsystemStates.Failing, $"資料庫版本 {database?.Version ?? "（沒有紀錄）"} 低於程式需要的 {release.MinimumDatabaseVersion}，請先執行資料庫更新。")
            : !snapshot ? (SubsystemStates.Idle, "這個資料庫沒有版本快照，無法偵測異動。")
            : changes.Count > 0 ? (SubsystemStates.Degraded, $"{changes.Count} 個物件與這一版應有的結構不同。")
            : (SubsystemStates.Healthy, "與這一版應有的結構一致。");
        IReadOnlyList<SubsystemFact> facts = [
            SubsystemFact.Words("app_version", "程式", release.Version),
            SubsystemFact.Words("database_version", "資料庫", database?.Version ?? "沒有紀錄", compatibility == ReleaseCompatibility.Incompatible ? SubsystemTones.Danger : SubsystemTones.Neutral),
            SubsystemFact.Count("changes", "異動物件", snapshot ? changes.Count : null, changes.Count > 0 ? SubsystemTones.Warning : SubsystemTones.Neutral),
        ];
        return new(status, summary, facts);
    }
}
