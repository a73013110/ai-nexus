using System.Text.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.Audit;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity.SelfService;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Features.Admin.Mail;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Platform.Account.Security;

/// <summary>帳號安全各 slice 共用：組出畫面資料、寫稽核與通知信、儲存後重發 session 與 CSRF token。</summary>
internal sealed class AccountSecurity(AppDbContext db, Reauthentication reauthentication, UserCredentials credentials, MailOutbox outbox, IAntiforgery csrf)
{
    public bool MailAvailable => outbox.Available;

    public AccountSecurityDto Describe(HttpContext http, User user)
    {
        WebSecurity.NoStore(http.Response);
        var methods = new List<string>();
        if (reauthentication.PasswordAvailable(http, user)) methods.Add("password");
        if (CodeAvailable(user)) methods.Add("code");
        var until = reauthentication.FreshUntil(http);
        var managed = user.Sid.StartsWith("managed:", StringComparison.Ordinal);
        return new(user.DisplayName, user.Account, user.AdAccount ?? (managed ? null : UserAccounts.AccountName(user.Account)), user.Email, user.EmailSource, user.EmailVerifiedAt,
            UserCredentials.EmailEditable(user),
            new(credentials.LocalLoginEditable, user.LocalEnabled && user.PasswordHash is not null, user.LocalAccount, user.PasswordChangedAt, !user.AdEnabled),
            reauthentication.Method(http), methods, until is null, until == DateTimeOffset.MaxValue ? null : until, outbox.Available,
            !Reauthentication.Allowed(http).IsSuccess);
    }

    /// <summary>可以把步升驗證碼寄到本人已驗證的信箱。</summary>
    public bool CodeAvailable(User user) => outbox.Available && user.Email is not null && user.EmailVerifiedAt is not null;

    /// <summary>稽核與變更同一交易寫入；細節只放遮罩後的信箱、帳號名與方式，絕不放密碼、驗證碼或雜湊。</summary>
    public void Audit(User user, string action, object details, string result = "success")
        => db.AuditEvents.Add(new AuditEvent { OwnerId = user.Id, ActorId = user.Id, ResourceId = user.Id, Action = action, Result = result, DetailsJson = JsonSerializer.Serialize(details) });

    /// <summary>寄到變更前已驗證的信箱；沒有時略過。與變更同一交易入列，變更失敗就不會寄出。</summary>
    public async Task NotifyAsync(User user, string? address, string change, string? detail, CancellationToken ct)
    {
        if (address is null || !outbox.Available) return;
        await outbox.EnqueueAsync(new AccountNoticeMail(), new AccountNoticeMailModel(user.DisplayName, change, detail),
            new MailRecipient(address, user.DisplayName, user.Id), new MailEnqueueOptions(RequestedBy: user.Id), ct);
    }

    /// <summary>目前已驗證、可收安全通知的信箱。</summary>
    public static string? VerifiedEmail(User user) => user.EmailVerifiedAt is null ? null : user.Email;

    /// <summary>
    /// 儲存變更（<c>SecurityVersion</c> 已遞增）後重發這個 session 的 cookie：其他 session 在下一個請求失效，這個保留。
    /// 並行修改造成的版本衝突回 <c>account_changed</c>，不覆蓋別人的變更。
    /// </summary>
    public async Task<Result<AccountSecurityChangeDto>> CommitAsync(HttpContext http, User user, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return AccountErrors.AccountChanged; }
        await reauthentication.ReissueAsync(http, user);
        return Changed(http, user);
    }

    /// <summary>claims 改變後舊的 CSRF token 失效，回傳依新 principal 產生的 token。</summary>
    public AccountSecurityChangeDto Changed(HttpContext http, User user, bool signedOut = false)
        => new(Describe(http, user), signedOut ? null : csrf.GetAndStoreTokens(http).RequestToken, signedOut);

    public static string Delivery(MailDelivery? delivery) => delivery?.Status ?? "unknown";
}
