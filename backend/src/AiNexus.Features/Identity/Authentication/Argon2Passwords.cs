using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace AiNexus.Features.Identity.Authentication;

/// <summary>新雜湊使用的 Argon2id 工作參數。正式環境一律用 <see cref="Recommended"/>，只有測試才註冊較低的成本。</summary>
public sealed record Argon2Cost(int MemoryKiB, int Iterations)
{
    public static readonly Argon2Cost Recommended = new(65536, 3);
}

/// <summary>PHC 格式的密碼雜湊（內含版本與參數）。記憶體與工作量有上下限，比對採固定時間以避免時序攻擊。</summary>
public sealed class Argon2Passwords(Argon2Cost cost) : IDisposable
{
    // 低於 OWASP 下限（19 MiB、2 次）的既有雜湊會被拒絕；但若目前設定的成本本身就低於下限，則以設定值為準，否則自己產生的雜湊也驗不過。
    private readonly int minimumMemoryKiB = Math.Min(19456, cost.MemoryKiB);
    private readonly int minimumIterations = Math.Min(2, cost.Iterations);
    private readonly SemaphoreSlim gate = new(2, 2);

    /// <summary>帳號不存在時仍以同樣的工作量比對這個假雜湊，避免從回應時間推測帳號是否存在。只建一次，不逐次配置。</summary>
    public string DummyHash { get; } = $"$argon2id$v=19$m={cost.MemoryKiB},t={cost.Iterations},p=1${Encode(new byte[16])}${Encode(new byte[32])}";

    /// <summary>本地密碼政策；不符時由呼叫端回傳 <c>invalid_password</c>。<see cref="HashAsync"/> 會要求先通過此檢查。</summary>
    public static bool IsAcceptable(string password) => password.Length is >= 12 and <= 128 && !password.All(char.IsWhiteSpace);

    public async Task<string> HashAsync(string password, CancellationToken ct)
    {
        if (!IsAcceptable(password)) throw new ArgumentException("Validate the password with IsAcceptable first.", nameof(password));
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = await DeriveAsync(password, salt, cost.MemoryKiB, cost.Iterations, 1, ct);
        try { return $"$argon2id$v=19$m={cost.MemoryKiB},t={cost.Iterations},p=1${Encode(salt)}${Encode(hash)}"; }
        finally { CryptographicOperations.ZeroMemory(hash); }
    }

    public async Task<(bool Valid, bool Upgrade)> VerifyAsync(string password, string encoded, CancellationToken ct)
    {
        if (password.Length is < 1 or > 128 || encoded.Length > 512) return (false, false);
        try
        {
            var parts = encoded.Split('$');
            if (parts.Length != 6 || parts[1] != "argon2id" || parts[2] != "v=19") return (false, false);
            var costs = parts[3].Split(',');
            if (costs.Length != 3 || !costs[0].StartsWith("m=", StringComparison.Ordinal) || !costs[1].StartsWith("t=", StringComparison.Ordinal) || !costs[2].StartsWith("p=", StringComparison.Ordinal)) return (false, false);
            var memory = int.Parse(costs[0][2..], CultureInfo.InvariantCulture);
            var iterations = int.Parse(costs[1][2..], CultureInfo.InvariantCulture);
            var parallelism = int.Parse(costs[2][2..], CultureInfo.InvariantCulture);
            if (memory < minimumMemoryKiB || memory > 131072 || iterations < minimumIterations || iterations > 8 || parallelism is < 1 or > 4) return (false, false);
            var salt = Decode(parts[4]); var expected = Decode(parts[5]);
            if (salt.Length is < 16 or > 64 || expected.Length != 32) return (false, false);
            var actual = await DeriveAsync(password, salt, memory, iterations, parallelism, ct);
            var valid = CryptographicOperations.FixedTimeEquals(actual, expected);
            CryptographicOperations.ZeroMemory(actual);
            return (valid, valid && (memory < cost.MemoryKiB || iterations < cost.Iterations));
        }
        catch (FormatException) { return (false, false); }
        catch (OverflowException) { return (false, false); }
    }

    private async Task<byte[]> DeriveAsync(string password, byte[] salt, int memory, int iterations, int parallelism, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        var bytes = Encoding.UTF8.GetBytes(password);
        try
        {
            using var algorithm = new Argon2id(bytes) { Salt = salt, MemorySize = memory, Iterations = iterations, DegreeOfParallelism = parallelism };
            // Argon2 套件的運算無法取消，所以必須等它跑完才釋放 gate，否則並行上限會被破壞。
            return await algorithm.GetBytesAsync(32);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=');
    private static byte[] Decode(string value) => Convert.FromBase64String(value.PadRight((value.Length + 3) / 4 * 4, '='));
}
