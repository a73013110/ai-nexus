using System.Globalization;
using AiNexus.Data.App.AccessControl;

namespace AiNexus.Features.Admin.AccessControl.Navigation;

/// <summary>
/// 外部連結的網址規則：絕對 <c>https://</c>、不可含使用者資訊，可有路徑與 query；主機須符合 <see cref="AccessControlOptions.NavigationLinkHosts"/>。
/// 格式在 request validator 檢查；允許清單在儲存時與產生 <c>/me</c> 時各檢查一次，因為設定可能在儲存之後改掉。
/// </summary>
public static class NavigationLinks
{
    /// <summary>網址格式；不看允許清單。</summary>
    public static bool WellFormed(string? url) => Parse(url) is not null;

    /// <summary>
    /// 主機是否在允許清單內。清單項目不帶 port 時只接受預設的 443：寫 <c>example.com</c> 不代表開放它的所有 port。
    /// <c>*.example.com</c> 只比對子網域，不含 example.com 本身。格式不合的網址一律不允許。
    /// </summary>
    public static bool HostAllowed(string? url, IEnumerable<string> patterns)
    {
        if (Parse(url) is not { } uri) return false;
        // IdnHost 是 punycode 形式，國際化網域不會因為 Unicode 大小寫或同形字而繞過清單。
        var host = uri.IdnHost.ToLowerInvariant();
        var port = uri.Port.ToString(CultureInfo.InvariantCulture);
        foreach (var pattern in patterns)
        {
            var colon = pattern.LastIndexOf(':');
            var name = colon >= 0 ? pattern[..colon] : pattern;
            if (colon >= 0 ? pattern[(colon + 1)..] != port : !uri.IsDefaultPort) continue;
            var matches = name.StartsWith("*.", StringComparison.Ordinal)
                ? host.Length > name.Length - 1 && host.EndsWith(name[1..], StringComparison.Ordinal)
                : host == name;
            if (matches) return true;
        }
        return false;
    }

    private static Uri? Parse(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > NavigationItem.UrlMaxLength || url.Any(c => char.IsControl(c) || char.IsWhiteSpace(c))) return null;
        // Uri 會接受 "https:/x" 之類的寬鬆寫法並自行補正，所以另外要求字面上就以 https:// 開頭。
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return null;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0
            && uri.HostNameType is UriHostNameType.Dns or UriHostNameType.IPv4 && uri.Host.Length > 0 ? uri : null;
    }
}
