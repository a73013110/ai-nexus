using Microsoft.Extensions.Caching.Memory;

namespace AiNexus.Data.App;

/// <summary>
/// 最近已稽核的查詢（身分＋動作＋條件），讓 <see cref="AuditTrail"/> 在時間窗內不重複記錄同一個查詢。
/// 只存在這個執行個體的記憶體：重新啟動後最多多記一筆，不影響「每個查詢至少有一筆」的保證。
/// </summary>
public sealed class RepeatedReads : IDisposable
{
    /// <summary>重複查詢合併的時間窗；畫面輪詢最長間隔遠小於此。</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 20_000 });

    public bool Seen(string key) => cache.TryGetValue(key, out _);

    public void Remember(string key) => cache.Set(key, true, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Window, Size = 1 });

    public void Dispose() => cache.Dispose();
}
