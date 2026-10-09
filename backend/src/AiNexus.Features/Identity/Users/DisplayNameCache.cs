using System.Collections.Concurrent;

namespace AiNexus.Features.Identity.Users;

/// <summary>Directory display names by SID for an hour, bounded so a host's lifetime cannot grow it without limit.</summary>
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
        if (names.Count >= Capacity)
        {
            foreach (var entry in names) if (now - entry.Value.At >= Lifetime) names.TryRemove(entry);
            if (names.Count >= Capacity) names.Clear();
        }
        names[sid] = (name, now);
        return name;
    }
}
