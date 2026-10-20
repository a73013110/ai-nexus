using System.Collections.Frozen;
using AiNexus.Features.Admin.Identity.Authentication;

namespace AiNexus.Features.Admin.Identity.SelfService;

/// <summary>
/// 使用者自行設定的本地密碼，在長度（<see cref="Argon2Passwords.IsAcceptable"/>）之外再擋明顯好猜的：包含帳號名、單一字元重複、
/// 連續數字或鍵盤序列，以及常見密碼（NIST SP 800-63B 的黑名單比對，不分大小寫）。
/// 清單刻意只收 12 字元以上的項目（更短的已被長度規則擋下）：內嵌的 <c>common-passwords.txt</c> 是外洩密碼排行中前 10,000 筆
/// 12 字元以上的項目（來源見 <c>common-passwords.LICENSE.txt</c>），再加上下面手列的常見變化。
/// </summary>
public static class PasswordPolicy
{
    private const string ResourceName = "AiNexus.Features.Admin.Identity.SelfService.common-passwords.txt";

    private static readonly string[] Supplemental =
    [
        "password1234", "password12345", "password123456", "password123!", "passw0rd1234", "p@ssw0rd1234", "p@ssword1234",
        "qwertyuiop12", "qwertyuiop123", "qwerty123456", "1qaz2wsx3edc", "1q2w3e4r5t6y", "1q2w3e4r5t6y7u", "zaq12wsxcde3",
        "abc123456789", "abcd12345678", "abcdefghijkl", "iloveyou1234", "welcome12345", "letmein12345", "administrator",
        "administrator1", "changeme1234", "aa123456789a", "asdfghjkl123", "zxcvbnm12345", "qazwsxedcrfv", "superman1234",
        "monkey123456", "dragon123456", "football1234", "baseball1234", "sunshine1234", "princess1234", "trustno11234",
    ];

    // 第一次檢查時才載入（約一萬筆）；資源缺失是建置錯誤，直接拋出而不是默默放行。
    private static readonly Lazy<FrozenSet<string>> Common = new(LoadCommon);

    private static readonly string[] Sequences = ["0123456789012", "9876543210987", "abcdefghijklmnopqrstuvwxyz", "qwertyuiopasdfghjklzxcvbnm", "1qaz2wsx3edc4rfv"];

    /// <summary>不接受時回傳 false；呼叫端回 <c>password_weak</c>。長度與空白規則仍由 <see cref="Argon2Passwords.IsAcceptable"/> 檢查。</summary>
    public static bool IsStrong(string password, params string?[] names)
    {
        var lowered = password.ToLowerInvariant();
        if (Common.Value.Contains(password) || password.Distinct().Count() <= 2) return false;
        if (Sequences.Any(sequence => sequence.Contains(lowered, StringComparison.Ordinal))) return false;
        return !names.Any(name => name is { Length: >= 3 } && lowered.Contains(name.Trim().ToLowerInvariant(), StringComparison.Ordinal));
    }

    private static FrozenSet<string> LoadCommon()
    {
        using var stream = typeof(PasswordPolicy).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is missing.");
        using var reader = new StreamReader(stream);
        var entries = new List<string>(Supplemental);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0) entries.Add(line);
        }
        return entries.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }
}
