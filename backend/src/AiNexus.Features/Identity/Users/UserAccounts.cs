using AiNexus.Data.App.Identity;
using System.Text;
using System.Text.RegularExpressions;

namespace AiNexus.Features.Identity.Users;

public static class UserAccounts
{
    /// <summary>去掉網域前綴（<c>DOMAIN\</c>）與 UPN 後綴（<c>@domain</c>）後的目錄帳號名。</summary>
    public static string AccountName(string account) => account.Trim().Split('\\').Last().Split('@')[0];

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
