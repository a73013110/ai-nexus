using System.Globalization;
using System.Text.RegularExpressions;
using AiNexus.Platform.Time;
using Cronos;

namespace AiNexus.Platform.Scheduling;

/// <summary>
/// 任務什麼時候執行。錯過的時間（停站、暫停）只補跑一次，不逐次回補；同一任務不會重疊，下一輪一律在上一輪結束後才排。
/// </summary>
public abstract partial record Schedule
{
    /// <summary><c>interval</c>、<c>cron</c> 或 <c>startup</c>，前端依此決定倒數的呈現。</summary>
    public abstract string Kind { get; }

    /// <summary>給人看的排程，例如「每 1 小時」「每天 08:00」。</summary>
    public abstract string Description { get; }

    /// <summary>每次程序啟動時執行一次的任務；引擎只讓啟動它的程序認領這一輪。</summary>
    public bool RunsAtStartup => this is StartupSchedule;

    /// <summary>每輪的固定間隔；只有 interval 有值，前端用來畫倒數環。</summary>
    public virtual TimeSpan? Interval => null;

    /// <summary>任務第一次出現（資料庫還沒有這一列）時的執行時間。</summary>
    public abstract DateTimeOffset? First(DateTimeOffset now);

    /// <summary>一輪結束後的下次執行時間；null 表示不再依排程執行。</summary>
    public abstract DateTimeOffset? Next(DateTimeOffset finishedAt, bool failed);

    /// <summary>可設定的最短間隔。</summary>
    public static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(10);

    /// <summary>可設定的最長間隔；更久的排程用 cron。</summary>
    public static readonly TimeSpan MaxInterval = TimeSpan.FromDays(31);

    /// <summary>
    /// 解讀設定的排程：固定間隔（<c>hh:mm:ss</c>、<c>d.hh:mm:ss</c>，上一輪結束後再隔多久；新任務立即執行一次）
    /// 或五欄 cron（台灣時間，例如 <c>30 3 * * *</c>）。無法解讀或間隔超出範圍時回傳 null。
    /// </summary>
    public static Schedule? Parse(string value)
    {
        // 間隔必須寫出時分秒：單獨的 "5" 會被解析成 5 天。
        if (value.Contains(':', StringComparison.Ordinal) && TimeSpan.TryParseExact(value, "c", CultureInfo.InvariantCulture, out var interval))
            return interval >= MinInterval && interval <= MaxInterval ? new IntervalSchedule(interval) : null;
        if (!CronExpression.TryParse(value, out var expression)) return null;
        var daily = DailyCron().Match(value);
        var description = daily.Success
            ? string.Create(CultureInfo.InvariantCulture, $"每天 {int.Parse(daily.Groups["hour"].Value, CultureInfo.InvariantCulture):00}:{int.Parse(daily.Groups["minute"].Value, CultureInfo.InvariantCulture):00}")
            : "cron：" + value;
        return new CronSchedule(expression, description);
    }

    /// <summary>每次程式啟動時執行一次；失敗時隔 <paramref name="retryAfter"/> 重試，成功後直到下次啟動都不再執行。</summary>
    public static Schedule AtStartup(TimeSpan retryAfter) => new StartupSchedule(retryAfter);

    /// <summary>給人看的間隔：「30 秒」「5 分鐘」「1 小時 30 分」「2 天」。</summary>
    public static string Describe(TimeSpan span) => span switch
    {
        { TotalMinutes: < 1 } => string.Create(CultureInfo.InvariantCulture, $"{span.TotalSeconds:0} 秒"),
        { TotalHours: < 1 } => string.Create(CultureInfo.InvariantCulture, $"{span.TotalMinutes:0} 分鐘"),
        { TotalDays: < 1 } => span.Minutes == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{span.TotalHours:0} 小時")
            : string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalHours} 小時 {span.Minutes} 分"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{span.TotalDays:0.#} 天"),
    };

    [GeneratedRegex(@"^(?<minute>[0-5]?\d) (?<hour>[01]?\d|2[0-3]) \* \* \*$")]
    private static partial Regex DailyCron();
}

internal sealed record IntervalSchedule : Schedule
{
    private readonly TimeSpan interval;

    public IntervalSchedule(TimeSpan interval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(interval, MinInterval);
        this.interval = interval;
    }

    public override string Kind => "interval";
    public override string Description => "每 " + Describe(interval);
    public override TimeSpan? Interval => interval;
    public override DateTimeOffset? First(DateTimeOffset now) => now;
    public override DateTimeOffset? Next(DateTimeOffset finishedAt, bool failed) => finishedAt + interval;
}

internal sealed record CronSchedule : Schedule
{
    // 台灣沒有日光節約時間，固定位移的時區就夠，也不依賴伺服器的時區資料庫。
    private static readonly TimeZoneInfo Taipei = TimeZoneInfo.CreateCustomTimeZone("Taipei", TaipeiTime.Offset, "Taipei", "Taipei");
    private readonly CronExpression expression;
    private readonly string description;

    public CronSchedule(CronExpression expression, string description)
    {
        this.expression = expression;
        this.description = description;
    }

    public override string Kind => "cron";
    public override string Description => description;
    public override DateTimeOffset? First(DateTimeOffset now) => expression.GetNextOccurrence(now, Taipei);
    public override DateTimeOffset? Next(DateTimeOffset finishedAt, bool failed) => expression.GetNextOccurrence(finishedAt, Taipei);
}

internal sealed record StartupSchedule(TimeSpan RetryAfter) : Schedule
{
    public override string Kind => "startup";
    public override string Description => "每次啟動時";
    public override DateTimeOffset? First(DateTimeOffset now) => now;
    public override DateTimeOffset? Next(DateTimeOffset finishedAt, bool failed) => failed ? finishedAt + RetryAfter : null;
}
