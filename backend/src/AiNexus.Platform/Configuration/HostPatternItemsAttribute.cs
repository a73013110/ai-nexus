using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace AiNexus.Platform.Configuration;

/// <summary>
/// 主機允許清單的每個元素：小寫主機名稱，可在最前面加 <c>*.</c> 表示子網域、在最後加 <c>:port</c>；不可含 scheme、路徑或大寫。
/// 寫成大寫或帶 <c>https://</c> 多半是設定錯誤，比對時會悄悄不符，所以在啟動時就擋下而不是自動修正。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class HostPatternItemsAttribute : ValidationAttribute
{
    public HostPatternItemsAttribute() : base("Every {0} item must be a lowercase host name with an optional '*.' prefix and ':port', without scheme or path.") { }

    public override bool IsValid(object? value) => value is null || value is IEnumerable<string> items && items.All(Valid);

    public static bool Valid(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern) || pattern.Length > 260) return false;
        var host = pattern;
        var colon = pattern.LastIndexOf(':');
        if (colon >= 0)
        {
            var port = pattern[(colon + 1)..];
            if (port.Length is 0 or > 5 || !port.All(char.IsAsciiDigit)
                || !int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number is < 1 or > 65535) return false;
            host = pattern[..colon];
        }
        if (host.StartsWith("*.", StringComparison.Ordinal)) host = host[2..];
        if (host.Length is 0 or > 253) return false;
        return host.Split('.').All(label => label.Length is > 0 and <= 63 && label[0] != '-' && label[^1] != '-'
            && label.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-'));
    }
}
