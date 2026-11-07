using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using AiNexus.Data.App.Identity;

namespace AiNexus.Features.Admin.Identity.ApiClients;

/// <summary>外部系統金鑰的產生與雜湊。金鑰明碼只在產生的那次回應出現，資料庫只留 <see cref="Hash"/> 與末四碼。</summary>
internal static class ApiClientKeys
{
    /// <summary>
    /// 固定前綴讓秘密掃描工具（git、日誌）認得出這是 AiNexus 的金鑰；也讓驗證端不必查資料庫就能擋掉格式不對的值。
    /// </summary>
    public const string Prefix = "ainexus_";

    // 32 bytes 亂數的 base64url 是 43 個字元；驗證時先比長度，超長的 header 不必雜湊。
    private const int SecretBytes = 32;
    public static readonly int Length = Prefix.Length + 43;

    public sealed record Issued(string Key, byte[] Hash, string Hint);

    public static Issued Issue()
    {
        var key = Prefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretBytes));
        return new(key, Hash(key), key[^ApiClient.KeyHintLength..]);
    }

    /// <summary>
    /// 金鑰是 256 位元亂數，SHA-256 就足以防止從雜湊回推；不需要 Argon2 這類為了低熵密碼設計的慢速雜湊，
    /// 驗證才能每次以索引查詢完成。
    /// </summary>
    public static byte[] Hash(string key) => SHA256.HashData(Encoding.UTF8.GetBytes(key));

    public static bool WellFormed(string? key) => key is not null && key.Length == Length && key.StartsWith(Prefix, StringComparison.Ordinal);
}
