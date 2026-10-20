using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using AiNexus.Data.App.Mail;

namespace AiNexus.Features.Admin.Mail;

/// <summary>收件地址的正規化、遮罩與查詢雜湊。只接受單純的 <c>local@domain</c>：顯示名稱、引號、註解與多個地址都拒絕。</summary>
public static class MailAddresses
{
    /// <summary>
    /// 去掉前後空白後必須原樣就是 <see cref="MailAddress.Address"/>（不接受 <c>"名字" &lt;a@b&gt;</c>），網域含點且每段合法；網域轉小寫，local part 保留大小寫。
    /// </summary>
    public static bool TryNormalize(string? input, out string address)
    {
        address = "";
        var value = input?.Trim() ?? "";
        if (value.Length is 0 or > OutboundMessage.AddressMaxLength || value.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c is '"' or '<' or '>' or '(' or ')' or ',' or ';'))
            return false;
        if (!MailAddress.TryCreate(value, out var parsed) || parsed.Address != value || parsed.DisplayName.Length > 0) return false;
        var at = value.LastIndexOf('@');
        if (at <= 0 || !IsDomain(value[(at + 1)..].ToLowerInvariant())) return false;
        address = value[..at] + "@" + value[(at + 1)..].ToLowerInvariant();
        return true;
    }

    /// <summary>小寫或大寫皆可的 DNS 網域：至少兩段，每段 1–63 個英數或連字號，不以連字號開頭或結尾。</summary>
    public static bool IsDomain(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 253) return false;
        var labels = value.Split('.');
        return labels.Length >= 2 && labels.All(label => label.Length is > 0 and <= 63 && label[0] != '-' && label[^1] != '-' && label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'));
    }

    public static string Domain(string address) => address[(address.LastIndexOf('@') + 1)..].ToLowerInvariant();

    /// <summary>列表顯示用：<c>chen@company.com</c> → <c>c***n@company.com</c>；一個字元的 local part 只顯示 <c>*</c>。</summary>
    public static string Mask(string address)
    {
        var at = address.LastIndexOf('@');
        if (at <= 0) return "***";
        var local = address[..at];
        var masked = local.Length == 1 ? "*" : $"{local[0]}***{local[^1]}";
        var result = string.Concat(masked, "@", address[(at + 1)..].ToLowerInvariant());
        return result.Length > OutboundMessage.AddressMaxLength ? result[..OutboundMessage.AddressMaxLength] : result;
    }

    /// <summary>SHA-256(小寫地址)：供以地址搜尋的查詢鍵。公司地址可被猜出，所以不是匿名化。</summary>
    public static byte[] Hash(string address) => SHA256.HashData(Encoding.UTF8.GetBytes(address.Trim().ToLowerInvariant()));

    public static bool Allowed(string address, IReadOnlyCollection<string> domains) => domains.Count == 0 || domains.Contains(Domain(address), StringComparer.Ordinal);
}
