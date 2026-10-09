using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AiNexus.Platform.Errors;
using Konscious.Security.Cryptography;

namespace AiNexus.Features.Identity.Authentication;

/// <summary>Argon2id work for new hashes. Production always uses <see cref="Recommended"/>; only tests register a cheaper cost.</summary>
public sealed record Argon2Cost(int MemoryKiB, int Iterations)
{
    public static readonly Argon2Cost Recommended = new(65536, 3);
}

/// <summary>Versioned PHC hashes; bounded memory/work and constant-time verification.</summary>
public sealed class Argon2Passwords(Argon2Cost cost) : IDisposable
{
    // Stored hashes below the OWASP minimum (19 MiB, 2 passes) are rejected unless the configured cost is itself lower.
    private readonly int minimumMemoryKiB = Math.Min(19456, cost.MemoryKiB);
    private readonly int minimumIterations = Math.Min(2, cost.Iterations);
    private readonly SemaphoreSlim gate = new(2, 2);

    /// <summary>Invalid accounts still perform the same work without allocating a hash per request.</summary>
    public string DummyHash { get; } = $"$argon2id$v=19$m={cost.MemoryKiB},t={cost.Iterations},p=1${Encode(new byte[16])}${Encode(new byte[32])}";

    public static void Validate(string password)
    {
        if (password.Length is < 12 or > 128 || password.All(char.IsWhiteSpace))
            throw new ApiException(400, "invalid_password", "本地密碼需為 12–128 個字元，可使用長句與密碼管理器。");
    }

    public async Task<string> HashAsync(string password, CancellationToken ct)
    {
        Validate(password);
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
            // The library's work is not cancellable; keep its slot until it has finished.
            return await algorithm.GetBytesAsync(32);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=');
    private static byte[] Decode(string value) => Convert.FromBase64String(value.PadRight((value.Length + 3) / 4 * 4, '='));
}
