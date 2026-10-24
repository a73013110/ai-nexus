using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Features.Admin.Identity.Management;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Identity.Provisioning;

/// <summary>
/// 節點清單的一位人員與平台上的對應狀態（<see cref="DirectoryPersonStates"/>）。<c>Changes</c> 是以 AD 匯入時會改的欄位
/// （只有 <c>changed</c> 有值），規則與預覽相同；預覽仍以 SID 重新讀目錄重算，這裡只供畫面分類與提示。
/// </summary>
public sealed record DirectoryPersonDto(string Sid, string Dn, string Account, string? Upn, string DisplayName, string? Email, string? Department, string? Title,
    string Path, bool Disabled, string State, Guid? UserId, IReadOnlyList<DirectoryChange> Changes);

/// <summary><see cref="DirectoryPersonDto.State"/> 的值，判斷規則與 <see cref="DirectoryAccounts.MatchAsync"/> 相同。</summary>
public static class DirectoryPersonStates
{
    public const string New = "new";
    /// <summary>已有同一個 SID 的平台帳號，且有欄位與 AD 不同；匯入＝以 AD 更新。</summary>
    public const string Changed = "changed";
    /// <summary>已有同一個 SID 的平台帳號，欄位都與 AD 一致。</summary>
    public const string Synced = "synced";
    /// <summary>管理員預建、尚未綁定的同名帳號，匯入時綁定。</summary>
    public const string Prebuilt = "prebuilt";
    public const string Conflict = "conflict";
    public const string InvalidAccount = "invalid_account";
    public const string Deleted = "deleted";
}

/// <summary>
/// 管理員從 AD 匯入與更新使用者的共用流程：逐人「規劃 → 在稽核交易內重新規劃並寫入」。
/// 建立、綁定與欄位同步都交給 <see cref="DirectoryAccounts"/>（與首次登入同一份規則）；每人一筆稽核、一個交易，一人失敗不影響其他人。
/// 鎖的順序固定為 <see cref="IdentityWriteLock"/> → 行政寫入鎖（<see cref="AdministrativeAudit"/> 內），且身分鎖在交易外取得：
/// 登入只取身分鎖，所以不會互相等待成死結。
/// </summary>
internal sealed partial class DirectoryProvisioning(AppDbContext db, DirectoryAccounts accounts, AdministrativeAudit audit, IdentityWriteLock writeLock,
    IOptions<AdAuthenticationOptions> options, TimeProvider clock, ILogger<DirectoryProvisioning> logger)
{
    /// <summary>單次匯入、預覽或更新的人數上限：每人一個 Serializable 交易，上限讓一次操作維持在數秒內。</summary>
    public const int MaxBatch = 100;

    /// <summary>格式檢查：<c>S-1-</c> 開頭、只含英數與連字號；實際是否存在由目錄判斷。</summary>
    public static bool IsSid(string? value) => value is { Length: > 4 and <= 184 } && value.StartsWith("S-1-", StringComparison.Ordinal) && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    /// <summary>
    /// 節點人員的平台狀態：兩次批次查詢（SID、AD 帳號名），不逐人查詢。已連結的人以 <see cref="DirectoryAccounts.Changes"/> 算出差異，
    /// 改名是否被佔用也從同一批帳號名判斷，所以與預覽的結果一致。
    /// </summary>
    public async Task<IReadOnlyList<DirectoryPersonDto>> DescribeAsync(IReadOnlyList<DirectoryPerson> people, CancellationToken ct)
    {
        var sids = people.Select(x => x.Sid).ToArray();
        var linked = (await db.Users.AsNoTracking().Where(x => sids.Contains(x.Sid)).ToListAsync(ct)).ToDictionary(x => x.Sid, StringComparer.OrdinalIgnoreCase);
        var names = people.Select(x => UserAccounts.IsValid(x.AccountName) ? UserAccounts.Normalize(x.AccountName) : null).OfType<string>().Distinct().ToArray();
        var named = (await db.Users.AsNoTracking().Where(x => x.AdAccount != null && names.Contains(x.AdAccount)).Select(x => new { x.Id, x.Sid, x.AdAccount }).ToListAsync(ct))
            .ToDictionary(x => x.AdAccount!, StringComparer.Ordinal);
        return [.. people.Select(person =>
        {
            IReadOnlyList<DirectoryChange> changes = [];
            string state; Guid? id = null;
            if (linked.TryGetValue(person.Sid, out var user))
            {
                id = user.Id;
                if (user.DeletedAt is not null) state = DirectoryPersonStates.Deleted;
                else
                {
                    var account = AccountOf(person, user);
                    var taken = DirectoryAccounts.RenamedAdAccount(user, account) is { } renamed && named.TryGetValue(renamed, out var holder) && holder.Id != user.Id;
                    changes = DirectoryAccounts.Changes(user, account, Profile(person), taken);
                    state = changes.Count > 0 ? DirectoryPersonStates.Changed : DirectoryPersonStates.Synced;
                }
            }
            else if (!UserAccounts.IsValid(person.AccountName)) state = DirectoryPersonStates.InvalidAccount;
            else if (named.TryGetValue(UserAccounts.Normalize(person.AccountName)!, out var holder))
            {
                state = holder.Sid.StartsWith(UserAccounts.ManagedSidPrefix, StringComparison.Ordinal) ? DirectoryPersonStates.Prebuilt : DirectoryPersonStates.Conflict;
                id = holder.Id;
            }
            else state = DirectoryPersonStates.New;
            return new DirectoryPersonDto(person.Sid, person.Dn, person.AccountName, person.Upn, person.DisplayName, person.Email, person.Department, person.Title,
                person.Path, person.Disabled, state, id, changes);
        })];
    }

    /// <summary>預覽一位人員：只讀，不寫入也不取鎖。</summary>
    public async Task<DirectoryOutcomeDto> PreviewAsync(DirectoryPerson person, bool includeDisabled, CancellationToken ct)
    {
        try { return Describe(await PlanAsync(person, includeDisabled, allowCreate: true, ct)); }
        finally { db.ChangeTracker.Clear(); }
    }

    /// <summary>
    /// 套用一位人員：取身分鎖 → 規劃 → 需要寫入時在 <see cref="AdministrativeAudit.MutateAsync"/> 的交易內重新規劃再寫入（稽核 <paramref name="action"/>）。
    /// 交易內的規劃與交易外不同（預覽後資料被改）時不寫入，回 <c>directory_changed</c>。<paramref name="allowCreate"/> 為 false 時只更新既有帳號。
    /// </summary>
    public async Task<DirectoryOutcomeDto> ApplyAsync(Guid actor, string feature, string action, DirectoryPerson person, bool includeDisabled, bool allowCreate, CancellationToken ct)
    {
        await writeLock.Gate.WaitAsync(ct);
        try
        {
            var plan = await PlanAsync(person, includeDisabled, allowCreate, ct);
            if (!plan.Writes) return Describe(plan);
            var id = plan.User?.Id ?? Guid.NewGuid();
            Plan? applied = null;
            var saved = await audit.MutateAsync(actor, feature, action, id, person.Sid, token => SnapshotAsync(id, token), async () =>
            {
                var fresh = await PlanAsync(person, includeDisabled, allowCreate, ct);
                if (!fresh.Writes || fresh.Outcome != plan.Outcome || (fresh.User is not null && fresh.User.Id != id)) return IdentityErrors.DirectoryChanged;
                var now = clock.GetUtcNow();
                if (fresh.Outcome == DirectoryOutcomes.Create) fresh = fresh with { User = accounts.Create(id, person.Sid, fresh.Account, Profile(person), now) };
                else DirectoryAccounts.Apply(fresh.User!, fresh.Changes, now);
                applied = fresh;
                return Result.Success;
            }, ct);
            return saved.IsSuccess ? Describe(applied!) : Describe(plan) with { Outcome = DirectoryOutcomes.Failed, ErrorCode = saved.Error.Code };
        }
        finally
        {
            db.ChangeTracker.Clear();
            writeLock.Gate.Release();
        }
    }

    /// <summary>沒有目錄資料可比對的使用者（目錄找不到、沒有綁定 AD、已刪除）：以平台資料回報。</summary>
    public static DirectoryOutcomeDto Unmatched(User user, string outcome)
        => new(user.Sid, user.Id, UserAccounts.AccountName(user.Account), user.DisplayName, null, "", false, user.Enabled, outcome, []);

    /// <summary>選取時還在、送出時目錄已找不到的 SID；姓名由畫面沿用選取時的資料顯示。</summary>
    public static DirectoryOutcomeDto Missing(string sid) => new(sid, null, "", "", null, "", false, null, DirectoryOutcomes.NotFound, []);

    /// <summary>一批匯入或更新結束：只記寫入的人數，誰被改了看稽核。</summary>
    public void Completed(string kind, IReadOnlyList<DirectoryOutcomeDto> items)
    {
        var saved = items.Count(x => x.Outcome is DirectoryOutcomes.Create or DirectoryOutcomes.Bind or DirectoryOutcomes.Update);
        LogCompleted(logger, kind, saved);
    }

    /// <summary>
    /// 規劃一位人員會發生什麼事。先清空追蹤狀態：<see cref="DirectoryAccounts.MatchAsync"/> 綁定預建帳號時會改追蹤中的 SID，
    /// 殘留到下一次規劃會被誤判為衝突。完整帳號沿用既有的寫法（Windows 模式是 <c>DOMAIN\sam</c>），只在帳號名改了才改成 <c>sam@Domain</c>，
    /// 避免與登入互相改來改去。
    /// </summary>
    private async Task<Plan> PlanAsync(DirectoryPerson person, bool includeDisabled, bool allowCreate, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var account = $"{person.AccountName}@{options.Value.Domain}";
        var match = await accounts.MatchAsync(person.Sid, account, ct);
        if (!match.IsSuccess)
            return new(person, match.Error.Code == IdentityErrors.AdBindingConflict.Code ? DirectoryOutcomes.Conflict : DirectoryOutcomes.InvalidAccount, null, account, []);
        var user = match.Value.Existing;
        if (user is null)
        {
            if (!allowCreate) return new(person, DirectoryOutcomes.NotFound, null, account, []);
            return new(person, person.Disabled && !includeDisabled ? DirectoryOutcomes.Disabled : DirectoryOutcomes.Create, null, account, []);
        }
        if (user.DeletedAt is not null) return new(person, DirectoryOutcomes.Deleted, user, account, []);
        if (match.Value.Bound && person.Disabled && !includeDisabled) return new(person, DirectoryOutcomes.Disabled, user, account, []);
        if (!match.Value.Bound) account = AccountOf(person, user);
        var changes = await accounts.ChangesAsync(user, account, Profile(person), ct);
        var outcome = match.Value.Bound ? DirectoryOutcomes.Bind : changes.Count > 0 ? DirectoryOutcomes.Update : DirectoryOutcomes.Unchanged;
        return new(person, outcome, user, account, changes);
    }

    /// <summary>已連結的帳號沿用原本的完整帳號寫法，只有帳號名改了才換成 <c>sam@Domain</c>。</summary>
    private string AccountOf(DirectoryPerson person, User user)
        => string.Equals(UserAccounts.AccountName(user.Account), person.AccountName, StringComparison.OrdinalIgnoreCase) ? user.Account : $"{person.AccountName}@{options.Value.Domain}";

    private static DirectoryProfile Profile(DirectoryPerson person) => new(person.DisplayName, person.Email);

    private static DirectoryOutcomeDto Describe(Plan plan) => new(plan.Person.Sid, plan.User?.Id, plan.Person.AccountName, plan.Person.DisplayName, plan.Person.Email,
        plan.Person.Path, plan.Person.Disabled, plan.User?.Enabled, plan.Outcome, plan.Changes);

    /// <summary><c>admin.user_import</c>／<c>admin.user_sync</c> 的稽核快照；信箱只記來源（地址由稽核遮罩器去除）。</summary>
    private async Task<object?> SnapshotAsync(Guid id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return null;
        return new
        {
            user.Account,
            user.AdAccount,
            user.DisplayName,
            user.Enabled,
            user.DeletedAt,
            source = user.EmailSource,
            roleIds = await SetUserRoles.RoleIdsAsync(db, id, ct),
        };
    }

    private sealed record Plan(DirectoryPerson Person, string Outcome, User? User, string Account, IReadOnlyList<DirectoryChange> Changes)
    {
        public bool Writes => Outcome is DirectoryOutcomes.Create or DirectoryOutcomes.Bind or DirectoryOutcomes.Update;
    }

    [LoggerMessage(EventId = 7002, EventName = "identity.directory_provisioned", Level = LogLevel.Information,
        Message = "Directory {Kind} finished with {Count} users saved.")]
    private static partial void LogCompleted(ILogger logger, string kind, int count);
}
