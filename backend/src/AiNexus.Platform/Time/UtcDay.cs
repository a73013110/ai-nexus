namespace AiNexus.Platform.Time;

/// <summary>以 UTC 為基準計算每日起點，避免統計與計費在跨時區換算時出現日界誤差。</summary>
public static class UtcDay
{
    /// <summary>取得指定時間點所在 UTC 日的 00:00:00。</summary>
    public static DateTimeOffset Start(DateTimeOffset instant) => new(instant.UtcDateTime.Date, TimeSpan.Zero);
}
