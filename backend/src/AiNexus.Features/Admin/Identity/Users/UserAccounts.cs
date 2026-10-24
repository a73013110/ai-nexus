using AiNexus.Data.App.Identity;
using System.Text;
using System.Text.RegularExpressions;

namespace AiNexus.Features.Admin.Identity.Users;

public static class UserAccounts
{
    /// <summary>還沒以 SID 綁定 AD 的帳號（管理員手建或預建）的 SID 前綴；AD 驗證或匯入對上 <c>AdAccount</c> 後改寫成真正的 SID。</summary>
    public const string ManagedSidPrefix = "managed:";

    public static string ManagedSid(Guid id) => ManagedSidPrefix + id;

    /// <summary>是否已以 SID 綁定 AD；查詢裡用 <c>x.Sid.StartsWith(ManagedSidPrefix)</c>。</summary>
    public static bool DirectoryBound(User user) => !user.Sid.StartsWith(ManagedSidPrefix, StringComparison.Ordinal);

    /// <summary>去掉網域前綴（<c>DOMAIN\</c>）與 UPN 後綴（<c>@domain</c>）後的目錄帳號名。</summary>
    public static string AccountName(string account) => account.Trim().Split('\\').Last().Split('@')[0];

    /// <summary>
    /// AD 表單登入輸入的容忍：去掉 <c>DOMAIN\</c> 前綴；只有後綴等於 AD 網域或 <paramref name="suffixes"/> 之一時才去掉 <c>@後綴</c>。
    /// 其他輸入原樣回傳，由 <c>LdapAuthenticator</c> 的格式檢查以帳密錯誤拒絕，不透露是格式問題。
    /// </summary>
    public static string DirectoryLoginName(string input, string domain, IReadOnlyCollection<string> suffixes)
    {
        var value = input.Trim();
        var slash = value.IndexOf('\\');
        if (slash > 0 && slash == value.LastIndexOf('\\')) value = value[(slash + 1)..];
        var at = value.LastIndexOf('@');
        if (at <= 0) return value;
        var suffix = value[(at + 1)..];
        return string.Equals(suffix, domain, StringComparison.OrdinalIgnoreCase) || suffixes.Contains(suffix, StringComparer.OrdinalIgnoreCase) ? value[..at] : value;
    }

    /// <summary>通過 <see cref="IsValid"/> 的帳號所對應的儲存形式（去空白、Unicode FormC、大寫）；未設定時回傳 <see langword="null"/>。</summary>
    public static string? Normalize(string? account)
    {
        if (string.IsNullOrWhiteSpace(account)) return null;
        var value = account.Trim().Normalize(NormalizationForm.FormC);
        if (!Matches(value)) throw new ArgumentException("Validate the account with IsValid first.", nameof(account));
        return value.ToUpperInvariant();
    }

    /// <summary>判斷 <see cref="Normalize"/> 是否接受此帳號；空白代表「未設定」，同樣視為有效。</summary>
    public static bool IsValid(string? account) => string.IsNullOrWhiteSpace(account) || Matches(account.Trim().Normalize(NormalizationForm.FormC));

    private static bool Matches(string value) => Regex.IsMatch(value, @"^[\p{L}\p{N}][\p{L}\p{N}._-]{0,63}$");

    public static UserAuthenticationDto Authentication(User user) => new(user.AdEnabled, user.LocalEnabled, user.AdAccount, user.LocalAccount, user.PasswordHash is not null);
}
