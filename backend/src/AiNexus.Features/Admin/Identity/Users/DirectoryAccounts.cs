using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity.Ordering;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.Users;

/// <summary>目錄資料會改動的一個欄位：<c>adAccount</c>、<c>displayName</c> 或 <c>email</c>。</summary>
public sealed record DirectoryChange(string Field, string? Before, string? After);

/// <summary>SID 對應的結果：<paramref name="Existing"/> 為 null 表示要新建；預建帳號已在追蹤中改好 SID，儲存後即綁定。</summary>
public sealed record DirectoryMatch(User? Existing, bool Bound);

/// <summary>
/// AD 身分與平台使用者的對應與同步規則。首次登入（<see cref="CurrentUser"/>）、管理員匯入與「從 AD 更新」共用這一份，
/// 三條路徑建立的帳號（AdAccount、member 角色）與同步的欄位（AD 帳號、姓名、信箱）因此一致。
/// 呼叫端負責取 <see cref="IdentityWriteLock"/>、寫稽核與儲存。
/// </summary>
public sealed partial class DirectoryAccounts(AppDbContext db, ILogger<DirectoryAccounts> logger)
{
    /// <summary>
    /// 依 SID 找使用者（帳號可能改名，SID 不會變）；找不到時依 AD 帳號名找管理員預建、尚未綁定（<c>managed:</c>）的帳號並改寫 SID。
    /// 帳號名已被其他 AD 身分使用回 <c>ad_binding_conflict</c>；帳號名不符平台規則回 <c>invalid_account</c>。
    /// 已刪除的使用者照樣回傳，由呼叫端決定如何處理（登入拒絕、匯入略過）。
    /// </summary>
    public async Task<Result<DirectoryMatch>> MatchAsync(string sid, string account, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Sid == sid, ct);
        if (user is not null) return new DirectoryMatch(user, false);
        var name = UserAccounts.AccountName(account);
        if (!UserAccounts.IsValid(name) || string.IsNullOrWhiteSpace(name)) return IdentityErrors.InvalidAccount;
        var adAccount = UserAccounts.Normalize(name);
        user = await db.Users.SingleOrDefaultAsync(x => x.AdAccount == adAccount, ct);
        if (user is null) return new DirectoryMatch(null, false);
        if (UserAccounts.DirectoryBound(user)) return IdentityErrors.AdBindingConflict;
        user.Sid = sid;
        return new DirectoryMatch(user, true);
    }

    /// <summary>
    /// 新增 AD 使用者與 <c>member</c> 角色（不儲存）。AdAccount 寫入與管理端預建相同的正規化值：出勤以它對應公文帳號，留空就查不到假單。
    /// 呼叫端已以 <see cref="MatchAsync"/> 確認沒有其他使用者佔用這個 AdAccount。順序接在已排序者之後（<see cref="NewUserOrder"/>）。
    /// </summary>
    public async Task<User> CreateAsync(Guid id, string sid, string account, DirectoryProfile profile, DateTimeOffset now, CancellationToken ct)
    {
        var user = new User { Id = id, Sid = sid, AdAccount = UserAccounts.Normalize(UserAccounts.AccountName(account)), DisplayName = profile.DisplayName, LastSeenAt = now };
        if (DirectoryEmail(profile.Email) is { } email) { user.Email = email; user.EmailSource = EmailSources.Directory; user.EmailVerifiedAt = now; }
        db.Users.Add(user);
        db.Set<UserRole>().Add(new UserRole { UserId = user.Id, RoleId = BuiltInAccess.MemberRole });
        await NewUserOrder.AppendAsync(db, user, ct);
        return user;
    }

    /// <summary>
    /// 目錄資料會改動哪些欄位（不寫入）；<see cref="Apply"/> 依同一份結果寫入，所以預覽與實際更新一致。
    /// 規則：AD 帳號名改了（含只改大小寫）就更新 AdAccount（新名稱已被他人使用時保留舊值並記警告）；
    /// 管理者維護個人資料（ProfileManaged）時不動姓名與信箱；信箱只覆寫空的或來源是目錄的值。
    /// </summary>
    public async Task<IReadOnlyList<DirectoryChange>> ChangesAsync(User user, string account, DirectoryProfile profile, CancellationToken ct)
    {
        var renamed = RenamedAdAccount(user, account);
        var taken = renamed is not null && await db.Users.AnyAsync(x => x.Id != user.Id && x.AdAccount == renamed, ct);
        if (taken) LogRenameConflict(logger, user.Id, "ad_account_in_use");
        return Changes(user, account, profile, taken);
    }

    /// <summary>AD 帳號名改了時的新 AdAccount（正規化後）；只改大小寫也算，讓畫面跟著目錄。沒改或名稱不符平台規則時為 null。</summary>
    public static string? RenamedAdAccount(User user, string account)
    {
        var name = UserAccounts.AccountName(account);
        return user.AdAccount is not null && UserAccounts.IsValid(name) && UserAccounts.Normalize(name) is { } renamed && renamed != user.AdAccount ? renamed : null;
    }

    /// <summary>
    /// <see cref="ChangesAsync"/> 的純計算版本：<paramref name="renameTaken"/> 由呼叫端查好（列表一次批次查，不逐人查詢）。
    /// </summary>
    public static IReadOnlyList<DirectoryChange> Changes(User user, string account, DirectoryProfile profile, bool renameTaken)
    {
        var changes = new List<DirectoryChange>();
        // 新名稱已被另一位使用者佔用（通常是已離職、名稱被 AD 重新配發的舊帳號）：AdAccount 維持原值，
        // 否則兩位使用者會指向同一個 AD 帳號，之後管理端編輯也會被 ad_binding_immutable 擋下。
        if (RenamedAdAccount(user, account) is { } renamed && !renameTaken) changes.Add(new("adAccount", user.AdAccount, renamed));
        if (user.ProfileManaged) return changes;
        if (user.DisplayName != profile.DisplayName) changes.Add(new("displayName", user.DisplayName, profile.DisplayName));
        var email = DirectoryEmail(profile.Email);
        if (OwnsEmail(user) && user.Email != email) changes.Add(new("email", user.Email, email));
        return changes;
    }

    /// <summary>寫入 <see cref="ChangesAsync"/> 算出的變更；目錄信箱寫入即視為已驗證，目錄移除 <c>mail</c> 時一併清空。</summary>
    public static void Apply(User user, IEnumerable<DirectoryChange> changes, DateTimeOffset now)
    {
        foreach (var change in changes)
        {
            switch (change.Field)
            {
                case "adAccount": user.AdAccount = change.After; break;
                case "displayName": user.DisplayName = change.After!; break;
                case "email":
                    user.Email = change.After;
                    user.EmailSource = change.After is null ? null : EmailSources.Directory;
                    user.EmailVerifiedAt = change.After is null ? null : now;
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(changes), change.Field, "Unknown directory field.");
            }
        }
    }

    /// <summary>登入的快速路徑用：姓名或信箱與目錄是否不同（不查資料庫，所以不含 AdAccount 改名，改名由 5 分鐘一次的完整同步處理）。</summary>
    public static bool Differs(User user, DirectoryProfile profile)
        => (!user.ProfileManaged && (user.DisplayName != profile.DisplayName || (OwnsEmail(user) && user.Email != DirectoryEmail(profile.Email))));

    /// <summary>目錄只管理「沒有信箱」或「來源就是目錄」的信箱；本人驗證或管理者設定的值，以及管理者維護個人資料的使用者，目錄都不覆寫。</summary>
    public static bool OwnsEmail(User user) => user.EmailSource is null or EmailSources.Directory;

    /// <summary>目錄的 <c>mail</c> 正規化後的值；格式不合（例如含顯示名稱）視為沒有信箱。</summary>
    public static string? DirectoryEmail(string? mail) => UserEmails.TryNormalize(mail, out var address) ? address : null;

    [LoggerMessage(EventId = 7001, EventName = "identity.directory_rename_skipped", Level = LogLevel.Warning,
        Message = "Directory account rename for user {UserId} was skipped: {Reason}.")]
    private static partial void LogRenameConflict(ILogger logger, Guid userId, string reason);
}
