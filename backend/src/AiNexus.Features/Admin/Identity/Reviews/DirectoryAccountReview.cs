using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity.Provisioning;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Accounts;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.Reviews;

/// <summary>
/// AD 已停用或找不到、平台帳號仍啟用的人：提示管理員停用平台帳號（ADR 0019）。只提示、不自動停用，理由同 <see cref="SyncDirectoryUsers"/>。
/// 每次讀取都查目錄，不另存「AD 狀態」：停用 AD 的人本來就不會再 AD 登入，要找的正是改用本地密碼或不再登入的人。
/// </summary>
internal sealed class DirectoryAccountReview(AppDbContext db, IAdDirectory directory) : IAccountReviewSource
{
    public const string Disabled = "identity.directory_disabled";
    public const string Missing = "identity.directory_missing";

    // 查不到的人超過這個數量與比例時，視為目錄查詢不完整（搜尋根、服務帳號權限、DC 複寫），整批不列：
    // 誤判會讓管理員照著待辦停用一大批在職的人。AD 明確標示停用的人不受影響。
    private const int MissingFloor = 5;
    private const double MissingRatio = 0.1;

    public async Task<AccountReviewReading> ReadAsync(CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().Where(ActiveUsers.Active).Where(x => !x.Sid.StartsWith(UserAccounts.ManagedSidPrefix))
            .Select(x => new { x.Id, x.Sid, x.LocalEnabled }).ToListAsync(ct);
        if (users.Count == 0) return new([]);
        IReadOnlyList<DirectoryPerson> people;
        try { people = await directory.FindAsync([.. users.Select(x => x.Sid)], ct); }
        catch (ExternalServiceException error)
        {
            return new([], error.Error.Code == IdentityErrors.DirectoryNotConfigured.Code
                ? "未設定 AD 服務帳號，無法檢查 AD 已停用的帳號。"
                : "AD 目錄暫時無法讀取，這次沒有檢查 AD 已停用的帳號。");
        }

        var found = people.ToDictionary(x => x.Sid, StringComparer.OrdinalIgnoreCase);
        var missing = users.Where(x => !found.ContainsKey(x.Sid)).ToList();
        var suspicious = missing.Count > MissingFloor && missing.Count > users.Count * MissingRatio;
        var items = new List<AccountReviewItem>();
        foreach (var user in users)
        {
            var detail = user.LocalEnabled ? "仍可用本地密碼登入平台。" : "AD 登入已被擋下；平台帳號停用後各項資料仍保留。";
            if (found.TryGetValue(user.Sid, out var person))
            {
                if (person.Disabled) items.Add(new(user.Id, Disabled, "AD 帳號已停用，平台帳號仍啟用", detail));
            }
            else if (!suspicious) items.Add(new(user.Id, Missing, "AD 找不到這個帳號，平台帳號仍啟用", detail));
        }
        return new(items, suspicious ? $"AD 有 {missing.Count} 位使用者查不到，比例過高，可能是目錄查詢不完整，這次不列出「AD 找不到」。" : null);
    }
}
