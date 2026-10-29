using AiNexus.Data.App;
using AiNexus.Data.App.Releases;
using AiNexus.Features.Admin.Releases.SchemaChanges;
using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Admin.Releases.Versions;

/// <summary>
/// 版本頁的對照：程式、資料庫與程式需要的最低資料庫版本。<paramref name="Compatibility"/> 為 <c>compatible</c>、<c>incompatible</c>
/// 或 <c>unchecked</c>（程式未打包或沒有最低版本）。<paramref name="HasSnapshot"/> 為 false 時異動清單一定是空的：這個庫沒有執行過 update.sql 或建庫腳本。
/// </summary>
public sealed record ReleaseStatusDto(
    string AppVersion, string? AppRevision, DateTimeOffset? AppBuiltAt,
    string? DatabaseVersion, DateTimeOffset? DatabaseDeployedAt, string? MinimumDatabaseVersion, string Compatibility, bool HasSnapshot);

internal sealed class GetReleaseStatus(AppDbContext db, AppRelease release, SchemaChangeReader changes)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/release-status", (GetReleaseStatus handler, CancellationToken ct) => handler.HandleAsync(ct))
        .NotAudited("版號與部署時間，不含結構內容")
        .WithName("GetReleaseStatus");

    public async Task<ReleaseStatusDto> HandleAsync(CancellationToken ct)
    {
        var database = await ReleaseCompatibility.LatestAsync(db, DeploymentComponents.Database, ct);
        return new(release.Version, release.Revision, release.BuiltAt, database?.Version, database?.DeployedAt, release.MinimumDatabaseVersion,
            ReleaseCompatibility.Of(release.MinimumDatabaseVersion, database?.Version), await changes.HasSnapshotAsync(ct));
    }
}
