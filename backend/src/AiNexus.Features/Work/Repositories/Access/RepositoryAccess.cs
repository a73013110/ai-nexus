using AiNexus.Data.App;
using AiNexus.Data.App.Repositories;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Features.Admin.Scheduling;
using AiNexus.Features.Platform.Notifications;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Repositories.Access;

/// <summary>使用者對一個儲存庫的管理身分：負責人只能給 read／write 給有帳號的人，管理員都可以（ADR 0037）。</summary>
public enum RepositoryManager
{
    None,
    Steward,
    Administrator,
}

/// <summary>一筆要寫入的授權；人員授權給 <paramref name="UserId"/>，非人員帳號只給 <paramref name="Login"/>。</summary>
public sealed record RepositoryGrantSpec(Guid RepositoryId, Guid? UserId, string? Login, string Permission, DateTimeOffset? ExpiresAt, string Source)
{
    public Guid? RequestId { get; init; }
    public Guid? GrantedById { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// 儲存庫授權的寫入規則只寫一次（ADR 0037）：申請核准、負責人與管理員授予、撤銷都經過這裡，Gitea 一律由同步任務收斂。
/// 每次寫入都要求同步任務盡快跑一輪（呼叫端提交後才叫醒），並通知當事人。
/// </summary>
[Scoped]
public sealed class RepositoryAccess(AppDbContext db, AccessService access, ScheduledTasks tasks, NotificationService notifications, TimeProvider clock)
{
    /// <summary>通知的目標種類；連結打開這個儲存庫的權限頁（<c>NotificationDisplay.Link</c>）。</summary>
    public const string TargetKind = "repository-access";

    public async Task<RepositoryManager> ManagerAsync(Guid actor, Guid repositoryId, CancellationToken ct)
        => await access.HoldsAsync(actor, FeatureCatalog.RepositoriesAccessManage, ct) ? RepositoryManager.Administrator
            : await db.RepositoryStewards.AnyAsync(x => x.RepositoryId == repositoryId && x.UserId == actor, ct) ? RepositoryManager.Steward
            : RepositoryManager.None;

    /// <summary>同一儲存庫同一人（或同一登入名）目前有效的授權。</summary>
    public IQueryable<RepositoryGrant> Active(Guid repositoryId, Guid? userId, string? login)
        => db.RepositoryGrants.Where(x => x.RepositoryId == repositoryId && x.EndedAt == null
            && (userId != null ? x.UserId == userId : x.UserId == null && x.Login == login));

    /// <summary>
    /// 寫入一筆授權，取代同一人在同一庫的有效授權（記為 replaced，不移除協作者，由新授權接手）。
    /// 呼叫端必須在交易內並自己儲存：舊授權以 SQL 直接結束，才不會與新授權撞上「每人每庫一筆」的唯一索引。
    /// </summary>
    public async Task<RepositoryGrant> GrantAsync(RepositoryGrantSpec spec, Guid? actor, CancellationToken ct)
    {
        // 同一人的授權寫入排隊（申請核准、負責人與管理員授予可能同時發生），否則兩邊都結束舊的、各加一筆，撞上唯一索引。
        if (spec.UserId is { } grantee) await db.LockUserRowAsync(grantee, ct);
        var now = clock.GetUtcNow();
        await Active(spec.RepositoryId, spec.UserId, spec.Login).ExecuteUpdateAsync(p => p
            .SetProperty(x => x.EndedAt, now).SetProperty(x => x.EndedById, actor)
            .SetProperty(x => x.EndReason, RepositoryGrantEndReasons.Replaced).SetProperty(x => x.SyncState, RepositorySyncStates.Applied), ct);
        var grant = new RepositoryGrant
        {
            RepositoryId = spec.RepositoryId, UserId = spec.UserId, Login = spec.UserId is null ? spec.Login : null, Permission = spec.Permission,
            ExpiresAt = spec.ExpiresAt, Source = spec.Source, RequestId = spec.RequestId, GrantedById = spec.GrantedById, Reason = spec.Reason,
        };
        db.RepositoryGrants.Add(grant);
        await tasks.QueueRunAsync(RepositoryAccessSyncTask.Definition.Key, actor, ct);
        // 申請核准由簽核通知申請人；這裡只通知別人直接給的授權。
        if (spec.UserId is { } user && user != actor && spec.Source != RepositoryGrantSources.Request)
        {
            var repository = await NameAsync(spec.RepositoryId, ct);
            await NotifyAsync(user, spec.RepositoryId, $"granted:{grant.Id}", "success", $"已取得儲存庫權限：{repository}",
                $"你在 {repository} 有{PermissionName(spec.Permission)}權限了" + (spec.ExpiresAt is { } at ? $"，到期日 {ExpiryDate(at):yyyy-MM-dd}" : "") + "。Gitea 約幾分鐘內生效。", ct);
        }
        return grant;
    }

    /// <summary>撤銷（負責人、管理員）或本人放棄一筆有效授權，並要求同步任務盡快移除協作者。呼叫端儲存。</summary>
    public async Task EndAsync(RepositoryGrant grant, string reason, Guid actor, CancellationToken ct)
    {
        await CloseAsync(grant, reason, actor, ct);
        await tasks.QueueRunAsync(RepositoryAccessSyncTask.Definition.Key, actor, ct);
    }

    /// <summary>同步任務結束到期的授權；任務自己接著移除協作者，不另外要求執行。呼叫端儲存。</summary>
    internal Task ExpireAsync(RepositoryGrant grant, CancellationToken ct) => CloseAsync(grant, RepositoryGrantEndReasons.Expired, null, ct);

    private async Task CloseAsync(RepositoryGrant grant, string reason, Guid? actor, CancellationToken ct)
    {
        (grant.EndedAt, grant.EndedById, grant.EndReason, grant.SyncState, grant.SyncError) = (clock.GetUtcNow(), actor, reason, RepositorySyncStates.Pending, null);
        if (grant.UserId is not { } user || user == actor) return;
        var repository = await NameAsync(grant.RepositoryId, ct);
        var (title, body) = reason == RepositoryGrantEndReasons.Expired
            ? ($"儲存庫權限已到期：{repository}", $"你在 {repository} 的{PermissionName(grant.Permission)}權限已到期，需要時請重新申請。")
            : ($"儲存庫權限已移除：{repository}", $"你在 {repository} 的{PermissionName(grant.Permission)}權限已被移除。");
        await NotifyAsync(user, grant.RepositoryId, $"ended:{grant.Id}", "info", title, body, ct);
    }

    /// <summary>到期前提醒本人與負責人；每筆授權只提醒一次（通知鍵含授權 ID）。呼叫端儲存。</summary>
    public async Task NoticeExpiringAsync(RepositoryGrant grant, CancellationToken ct)
    {
        if (grant.ExpiresAt is not { } at) return;
        var repository = await NameAsync(grant.RepositoryId, ct);
        var holder = grant.UserId is { } user ? await db.Users.Where(x => x.Id == user).Select(x => x.DisplayName).SingleOrDefaultAsync(ct) : grant.Login;
        var stewards = await db.RepositoryStewards.Where(x => x.RepositoryId == grant.RepositoryId).Select(x => x.UserId).ToListAsync(ct);
        foreach (var owner in stewards.Append(grant.UserId ?? Guid.Empty).Where(x => x != Guid.Empty).Distinct())
            await NotifyAsync(owner, grant.RepositoryId, $"expiring:{grant.Id}", "info", $"儲存庫權限即將到期：{repository}",
                (owner == grant.UserId ? "你" : holder) + $"在 {repository} 的{PermissionName(grant.Permission)}權限將於 {ExpiryDate(at):yyyy-MM-dd} 後到期。需要繼續使用請重新申請或由負責人延長。", ct);
    }

    /// <summary>
    /// 現有授權已涵蓋要求的權限：等級相同以上，且不會比要求的更早到期。申請前擋重複申請、核准時保留現有授權，都用這個判斷，
    /// 所以申請「同權限、較晚到期」視為延長，可以送出。
    /// </summary>
    public static bool Covers(RepositoryGrant current, string permission, DateTimeOffset? expiresAt)
        => RepositoryPermissions.Rank(current.Permission) >= RepositoryPermissions.Rank(permission)
            && (current.ExpiresAt is null || (expiresAt is { } requested && current.ExpiresAt >= requested));

    /// <summary>畫面與通知的權限名稱。</summary>
    public static string PermissionName(string permission) => permission switch
    {
        RepositoryPermissions.Read => "讀取",
        RepositoryPermissions.Write => "寫入",
        RepositoryPermissions.Admin => "管理",
        _ => permission,
    };

    /// <summary>到期日（含當天，台北時間）換成到期時刻：隔天 00:00。</summary>
    public static DateTimeOffset ExpiresAt(DateOnly date) => TaipeiTime.At(date.AddDays(1), TimeOnly.MinValue);

    /// <summary><see cref="ExpiresAt"/> 的反向：畫面顯示的到期日。</summary>
    public static DateOnly ExpiryDate(DateTimeOffset expiresAt) => DateOnly.FromDateTime(expiresAt.ToTaipei().AddTicks(-1).DateTime);

    private Task<string> NameAsync(Guid repositoryId, CancellationToken ct)
        => db.ManagedRepositories.Where(x => x.Id == repositoryId).Select(x => x.Owner + "/" + x.Name).SingleAsync(ct);

    private Task NotifyAsync(Guid owner, Guid repositoryId, string eventKey, string severity, string title, string body, CancellationToken ct)
        => notifications.PublishAsync(new(owner, $"repository-access:{eventKey}", "repositories.access", severity, title, body, TargetKind, repositoryId), ct);
}
