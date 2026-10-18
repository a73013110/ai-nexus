using System.Collections.Concurrent;

namespace AiNexus.Features.Admin.Identity.Users;

/// <summary>依 SID 快取目錄顯示名稱一小時；容量有上限，避免程序長時間執行時無限制成長。</summary>
public sealed class DisplayNameCache(TimeProvider clock)
{
    private const int Capacity = 4096;
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);
    private readonly ConcurrentDictionary<string, (string Name, DateTimeOffset At)> names = new(StringComparer.Ordinal);

    public string GetOrAdd(string sid, Func<string> resolve)
    {
        var now = clock.GetUtcNow();
        if (names.TryGetValue(sid, out var cached) && now - cached.At < Lifetime) return cached.Name;
        var name = resolve();
        // 超過容量時先清掉過期項目，仍然滿就整個清空：寧可多查幾次目錄，也不讓快取無上限膨脹。
        if (names.Count >= Capacity)
        {
            foreach (var entry in names)
            {
                if (now - entry.Value.At >= Lifetime) names.TryRemove(entry);
            }
            if (names.Count >= Capacity) names.Clear();
        }
        names[sid] = (name, now);
        return name;
    }
}
