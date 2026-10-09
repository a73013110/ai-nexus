using System.Text;
using System.Text.RegularExpressions;

namespace AiNexus.Features.Identity.Users;

public static class UserAccounts
{
    /// <summary>The directory account without domain prefix or UPN suffix.</summary>
    public static string AccountName(string account) => account.Trim().Split('\\').Last().Split('@')[0];

    /// <summary>The stored form of an account that <see cref="IsValid"/> accepted; <see langword="null"/> when not set.</summary>
    public static string? Normalize(string? account)
    {
        if (string.IsNullOrWhiteSpace(account)) return null;
        var value = account.Trim().Normalize(NormalizationForm.FormC);
        if (!Matches(value)) throw new ArgumentException("Validate the account with IsValid first.", nameof(account));
        return value.ToUpperInvariant();
    }

    /// <summary>Whether <see cref="Normalize"/> accepts <paramref name="account"/>; an empty account means "not set".</summary>
    public static bool IsValid(string? account) => string.IsNullOrWhiteSpace(account) || Matches(account.Trim().Normalize(NormalizationForm.FormC));

    private static bool Matches(string value) => Regex.IsMatch(value, @"^[\p{L}\p{N}][\p{L}\p{N}._-]{0,63}$");

    public static UserAuthenticationDto Authentication(NexusUser user) => new(user.AdEnabled, user.LocalEnabled, user.AdAccount, user.LocalAccount, user.PasswordHash is not null);
}
