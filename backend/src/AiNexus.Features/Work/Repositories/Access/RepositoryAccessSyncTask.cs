using AiNexus.Data.App;
using AiNexus.Data.App.Repositories;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Scheduling;
using AiNexus.Platform.Strings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Repositories.Access;

/// <summary>
/// 儲存庫權限唯一寫入 Gitea 的地方（ADR 0037）。每輪：
/// 1. 目錄：服務帳號所屬組織的儲存庫以 GiteaId 對應，改名、轉移跟著更新；Gitea 已沒有的記 <c>MissingSince</c>，不動它的協作者。
/// 2. 到期：結束到期的授權，到期前 <see cref="GiteaOptions.ExpiryNoticeDays"/> 天提醒本人與負責人一次。
/// 3. 收斂：每個儲存庫讀一次協作者。有效授權缺的加上、待同步或超過 <see cref="GiteaOptions.ReapplyHours"/> 的重寫權限；
///    結束的授權移除協作者（同一登入名還有有效授權時不動）；沒有授權的協作者另存為未登記，不移除。
/// 每個儲存庫各自提交，中斷或與使用者的撤銷撞上時，下一輪依資料狀態接著做；單一儲存庫失敗不影響其他。
/// </summary>
public sealed partial class RepositoryAccessSyncTask(
    AppDbContext db, IGiteaServiceClient gitea, RepositoryAccess access, IOptions<GiteaOptions> options, TimeProvider clock,
    ILogger<RepositoryAccessSyncTask> logger) : IScheduledTask
{
    public static ScheduledTaskDefinition Definition { get; } = new(
        "repositories.access-sync", "儲存庫權限同步", "程式庫",
        "同步 Gitea 組織的儲存庫目錄，並把協作者調成與授權一致：缺的加上、撤銷或到期的移除，列出未登記的協作者。")
    {
        Unavailable = services => services.GetRequiredService<IOptions<GiteaOptions>>().Value.AccessEnabled
            ? null : "未設定 Gitea 服務帳號權杖（Repositories:Gitea:ServiceToken）。",
    };

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var self = await gitea.AccountAsync(ct);
        await SyncCatalogAsync(now, ct);
        var expired = await ExpireAsync(now, ct);
        var total = new Tally();
        var repositories = await db.ManagedRepositories.AsNoTracking().Where(x => x.MissingSince == null)
            .OrderBy(x => x.Owner).ThenBy(x => x.Name).ToListAsync(ct);
        foreach (var repository in repositories)
        {
            try { total += await ReconcileAsync(repository, self, now, ct); }
            catch (DbUpdateConcurrencyException) { total += new Tally(Deferred: 1); }
            catch (ExternalServiceException error)
            {
                LogRepositoryFailed(logger, repository.Id, error.Error.Code);
                total += new Tally(Failed: 1);
            }
            db.ChangeTracker.Clear();
        }
        var summary = $"{repositories.Count:N0} 個儲存庫：加入或更新 {total.Applied:N0}、移除 {total.Removed:N0}、到期 {expired:N0}"
            + (total.Failed > 0 ? $"、失敗 {total.Failed:N0}" : "") + (total.Deferred > 0 ? $"、{total.Deferred:N0} 個留待下一輪" : "");
        return total.Applied + total.Removed + total.Failed + total.Deferred + expired == 0 ? TaskOutcome.Idle(summary) : TaskOutcome.Done(summary);
    }

    /// <summary>
    /// 只在內容有變時寫入：設定頁以 RowVersion 防止覆蓋，每輪都更新會讓管理員一直撞上版本衝突。
    /// 組織一個都讀不到時視同 Gitea 異常、不標記任何儲存庫消失，避免權杖失效時整份目錄被當成刪除。
    /// </summary>
    private async Task SyncCatalogAsync(DateTimeOffset now, CancellationToken ct)
    {
        var organizations = await gitea.OrganizationsAsync(ct);
        if (organizations.Count == 0) return;
        var seen = new List<GiteaRepository>();
        foreach (var organization in organizations) seen.AddRange(await gitea.RepositoriesAsync(organization, ct));
        var known = await db.ManagedRepositories.ToDictionaryAsync(x => x.GiteaId, ct);
        foreach (var item in seen.Where(x => x.Id > 0 && x.Name.Length > 0).DistinctBy(x => x.Id))
        {
            var teams = (await gitea.TeamsAsync(item.Owner, item.Name, ct)).Where(x => !x.Equals("Owners", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase).ToList();
            var granting = teams.Count == 0 ? null : string.Join("、", teams).Clip(ManagedRepository.TeamsMaxLength);
            if (!known.TryGetValue(item.Id, out var row))
            {
                // 新儲存庫預設開放申請：申請仍要經過簽核，關閉由管理員決定。
                db.ManagedRepositories.Add(new ManagedRepository
                {
                    GiteaId = item.Id, Owner = item.Owner, Name = item.Name, Description = item.Description, IsPrivate = item.IsPrivate,
                    IsArchived = item.IsArchived, GrantingTeams = granting, Requestable = true, SyncedAt = now,
                });
                continue;
            }
            if ((row.Owner, row.Name, row.Description, row.IsPrivate, row.IsArchived, row.GrantingTeams, row.MissingSince)
                == (item.Owner, item.Name, item.Description, item.IsPrivate, item.IsArchived, granting, null)) continue;
            (row.Owner, row.Name, row.Description, row.IsPrivate, row.IsArchived, row.GrantingTeams, row.MissingSince, row.SyncedAt)
                = (item.Owner, item.Name, item.Description, item.IsPrivate, item.IsArchived, granting, null, now);
        }
        var ids = seen.Select(x => x.Id).ToHashSet();
        foreach (var row in known.Values.Where(x => x.MissingSince is null && !ids.Contains(x.GiteaId))) row.MissingSince = now;
        // 管理員同時改了設定：這輪的目錄更新放棄，下一輪重讀再比對；收斂仍用資料庫現有的目錄繼續。
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { }
        db.ChangeTracker.Clear();
    }

    /// <summary>結束到期的授權，並提醒即將到期的（通知鍵含授權 ID，每筆只送一次）。</summary>
    private async Task<int> ExpireAsync(DateTimeOffset now, CancellationToken ct)
    {
        var expired = await db.RepositoryGrants.Where(x => x.EndedAt == null && x.ExpiresAt <= now).ToListAsync(ct);
        foreach (var grant in expired) await access.ExpireAsync(grant, ct);
        var soon = now.AddDays(options.Value.ExpiryNoticeDays);
        foreach (var grant in await db.RepositoryGrants.AsNoTracking().Where(x => x.EndedAt == null && x.ExpiresAt > now && x.ExpiresAt <= soon).ToListAsync(ct))
            await access.NoticeExpiringAsync(grant, ct);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return expired.Count;
    }

    private async Task<Tally> ReconcileAsync(ManagedRepository repository, string self, DateTimeOffset now, CancellationToken ct)
    {
        var (owner, name) = (repository.Owner, repository.Name);
        var grants = await db.RepositoryGrants
            .Where(x => x.RepositoryId == repository.Id && (x.EndedAt == null || x.SyncState != RepositorySyncStates.Applied)).ToListAsync(ct);
        var userIds = grants.Where(x => x.UserId is not null).Select(x => x.UserId!.Value).Distinct().ToArray();
        var accounts = await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.AdAccount, ct);
        string? LoginOf(RepositoryGrant grant) => grant.UserId is { } user ? accounts.GetValueOrDefault(user) : grant.Login;

        var present = (await gitea.CollaboratorsAsync(owner, name, ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var desired = new Dictionary<string, RepositoryGrant>(StringComparer.OrdinalIgnoreCase);
        var tally = new Tally();
        var reapplyBefore = now.AddHours(-options.Value.ReapplyHours);
        foreach (var grant in grants.Where(x => x.EndedAt == null))
        {
            if (LoginOf(grant) is not { Length: > 0 } login)
            {
                (grant.SyncState, grant.SyncError) = (RepositorySyncStates.Failed, RepositoriesErrors.AccountMissing.Code);
                tally += new Tally(Failed: 1);
                continue;
            }
            desired[login] = grant;
            if (grant.SyncState == RepositorySyncStates.Applied && present.Contains(login) && grant.SyncedAt >= reapplyBefore) continue;
            tally += await ApplyAsync(grant, now, () => gitea.SetCollaboratorAsync(owner, name, login, grant.Permission, ct)) ? new Tally(Applied: 1) : new Tally(Failed: 1);
            present.Add(login);
        }
        foreach (var grant in grants.Where(x => x.EndedAt != null))
        {
            // 同一登入名已由有效授權接手、或 Gitea 上本來就沒有：沒有要移除的。
            if (LoginOf(grant) is not { Length: > 0 } login || desired.ContainsKey(login) || !present.Contains(login))
            {
                (grant.SyncState, grant.SyncedAt, grant.SyncError) = (RepositorySyncStates.Applied, now, null);
                continue;
            }
            if (await ApplyAsync(grant, now, () => gitea.RemoveCollaboratorAsync(owner, name, login, ct)))
            {
                present.Remove(login);
                tally += new Tally(Removed: 1);
            }
            else tally += new Tally(Failed: 1);
        }
        await RecordUnregisteredAsync(repository, present.Where(x => !desired.ContainsKey(x) && !x.Equals(self, StringComparison.OrdinalIgnoreCase)), now, ct);
        await db.SaveChangesAsync(ct);
        return tally;
    }

    /// <summary>Gitea 上有、AI Nexus 沒有授權的協作者；新出現的才讀一次權限（含團隊取得的有效權限，只供顯示）。</summary>
    private async Task RecordUnregisteredAsync(ManagedRepository repository, IEnumerable<string> logins, DateTimeOffset now, CancellationToken ct)
    {
        var current = logins.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = await db.RepositoryCollaborators.Where(x => x.RepositoryId == repository.Id).ToListAsync(ct);
        db.RepositoryCollaborators.RemoveRange(rows.Where(x => !current.Contains(x.Login)));
        foreach (var login in current.Except(rows.Select(x => x.Login), StringComparer.OrdinalIgnoreCase))
            db.RepositoryCollaborators.Add(new RepositoryCollaborator
            {
                RepositoryId = repository.Id, Login = login.Clip(RepositoryGrant.LoginMaxLength), ObservedAt = now,
                Permission = await gitea.PermissionAsync(repository.Owner, repository.Name, login, ct),
            });
    }

    private async Task<bool> ApplyAsync(RepositoryGrant grant, DateTimeOffset now, Func<Task> call)
    {
        try
        {
            await call();
            (grant.SyncState, grant.SyncedAt, grant.SyncError) = (RepositorySyncStates.Applied, now, null);
            return true;
        }
        catch (ExternalServiceException error) when (error.Error == RepositoriesErrors.AccountMissing || error.Error == RepositoriesErrors.NotFound)
        {
            // 只屬於這一筆的失敗（Gitea 沒有這個帳號）留在授權上給管理員看，下一輪再試；連線、權杖問題往上丟，整個儲存庫中止。
            (grant.SyncState, grant.SyncError) = (RepositorySyncStates.Failed, error.Error.Code);
            LogGrantFailed(logger, grant.Id, error.Error.Code);
            return false;
        }
    }

    private readonly record struct Tally(int Applied = 0, int Removed = 0, int Failed = 0, int Deferred = 0)
    {
        public static Tally operator +(Tally a, Tally b) => new(a.Applied + b.Applied, a.Removed + b.Removed, a.Failed + b.Failed, a.Deferred + b.Deferred);
    }

    [LoggerMessage(EventId = 3602, EventName = "repositories.access_grant_failed", Level = LogLevel.Warning, Message = "Repository grant {ResourceId} could not be applied to Gitea: {ErrorCode}.")]
    private static partial void LogGrantFailed(ILogger logger, Guid resourceId, string errorCode);

    [LoggerMessage(EventId = 3603, EventName = "repositories.access_repository_failed", Level = LogLevel.Warning, Message = "Repository {ResourceId} could not be reconciled with Gitea: {ErrorCode}.")]
    private static partial void LogRepositoryFailed(ILogger logger, Guid resourceId, string errorCode);
}
