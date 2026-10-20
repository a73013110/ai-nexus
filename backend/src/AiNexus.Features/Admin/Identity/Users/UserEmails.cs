using System.Net.Mail;
using AiNexus.Data.App.Identity;

namespace AiNexus.Features.Admin.Identity.Users;

/// <summary>
/// 使用者通知信箱的正規化與遮罩。規則與 <c>MailAddresses</c> 相同（只收單純的 <c>local@domain</c>），
/// 但 Identity 不能依賴 Mail 模組（Mail 依賴 Identity），所以在這裡各自實作；改規則時兩邊一起改。
/// </summary>
public static class UserEmails
{
    public static bool TryNormalize(string? input, out string address)
    {
        address = "";
        var value = input?.Trim() ?? "";
        if (value.Length is 0 or > EmailSources.AddressMaxLength || value.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c is '"' or '<' or '>' or '(' or ')' or ',' or ';'))
            return false;
        if (!MailAddress.TryCreate(value, out var parsed) || parsed.Address != value || parsed.DisplayName.Length > 0) return false;
        var at = value.LastIndexOf('@');
        var domain = value[(at + 1)..].ToLowerInvariant();
        var labels = domain.Split('.');
        if (at <= 0 || domain.Length > 253 || labels.Length < 2
            || !labels.All(label => label.Length is > 0 and <= 63 && label[0] != '-' && label[^1] != '-' && label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')))
            return false;
        address = value[..at] + "@" + domain;
        return true;
    }

    /// <summary>通知信與稽核用：<c>chen@company.com</c> → <c>c***n@company.com</c>。</summary>
    public static string Mask(string address)
    {
        var at = address.LastIndexOf('@');
        if (at <= 0) return "***";
        var local = address[..at];
        return (local.Length == 1 ? "*" : $"{local[0]}***{local[^1]}") + "@" + address[(at + 1)..].ToLowerInvariant();
    }

    /// <summary>比對同一地址：local part 大小寫在實務上不區分，所以整個地址不分大小寫。</summary>
    public static bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
