using System.Collections.Concurrent;

namespace AiNexus.Features.Admin.Identity.Users;

/// <summary>目錄提供的姓名與信箱（Windows 整合驗證時由 SID 查 AD）。</summary>
public sealed record DirectoryProfile(string DisplayName, string? Email);

/// <summary>依 SID 快取目錄的姓名與信箱一小時；容量有上限，避免程序長時間執行時無限制成長。</summary>
public sealed class DirectoryProfileCache(TimeProvider clock)
{
    private const int Capacity = 4096;
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);
    private readonly ConcurrentDictionary<string, (DirectoryProfile Profile, DateTimeOffset At)> profiles = new(StringComparer.Ordinal);

    public DirectoryProfile GetOrAdd(string sid, Func<DirectoryProfile> resolve)
    {
        var now = clock.GetUtcNow();
        if (profiles.TryGetValue(sid, out var cached) && now - cached.At < Lifetime) return cached.Profile;
        var profile = resolve();
        // 超過容量時先清掉過期項目，仍然滿就整個清空：寧可多查幾次目錄，也不讓快取無上限膨脹。
        if (profiles.Count >= Capacity)
        {
            foreach (var entry in profiles)
            {
                if (now - entry.Value.At >= Lifetime) profiles.TryRemove(entry);
            }
            if (profiles.Count >= Capacity) profiles.Clear();
        }
        profiles[sid] = (profile, now);
        return profile;
    }
}
