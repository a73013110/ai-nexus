using System.Text;
using System.Text.RegularExpressions;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Identity.Users;

public static class UserAccounts
{
    /// <summary>The directory account without domain prefix or UPN suffix.</summary>
    public static string AccountName(string account) => account.Trim().Split('\\').Last().Split('@')[0];

    public static string? Normalize(string? account)
    {
        if (string.IsNullOrWhiteSpace(account)) return null;
        var value = account.Trim().Normalize(NormalizationForm.FormC);
        if (!Matches(value))
            throw new ApiException(400, "invalid_account", "登入帳號需為 1–64 個字元，只能使用字母、數字、點、底線與連字號。AD 請填目錄中的帳號，不含網域。");
        return value.ToUpperInvariant();
    }

    /// <summary>Whether <see cref="Normalize"/> accepts <paramref name="account"/>; an empty account means "not set".</summary>
    internal static bool IsValid(string? account) => string.IsNullOrWhiteSpace(account) || Matches(account.Trim().Normalize(NormalizationForm.FormC));

    private static bool Matches(string value) => Regex.IsMatch(value, @"^[\p{L}\p{N}][\p{L}\p{N}._-]{0,63}$");

    public static UserAuthenticationDto Authentication(NexusUser user) => new(user.AdEnabled, user.LocalEnabled, user.AdAccount, user.LocalAccount, user.PasswordHash is not null);
}
