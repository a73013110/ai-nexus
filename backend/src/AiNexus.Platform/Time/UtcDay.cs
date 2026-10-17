namespace AiNexus.Platform.Time;

/// <summary>提供 UTC 基準的每日起點計算，消除統計與計費在跨時區運算時的邊界偏差。</summary>
public static class UtcDay
{
    /// <summary>取得指定時間點於 UTC 零時整（00:00:00）的時間戳記。</summary>
    public static DateTimeOffset Start(DateTimeOffset instant) => new(instant.UtcDateTime.Date, TimeSpan.Zero);
}
