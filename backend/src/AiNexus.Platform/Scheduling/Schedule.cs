using System.Globalization;
using AiNexus.Platform.Time;
using Cronos;

namespace AiNexus.Platform.Scheduling;

/// <summary>
/// 任務什麼時候執行。錯過的時間（停站、暫停）只補跑一次，不逐次回補；同一任務不會重疊，下一輪一律在上一輪結束後才排。
/// </summary>
public abstract record Schedule
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

    /// <summary>上一輪結束後隔 <paramref name="interval"/> 再執行；新任務立即執行一次。</summary>
    public static Schedule Every(TimeSpan interval) => new IntervalSchedule(interval);

    /// <summary>每天台灣時間 <paramref name="hour"/>:<paramref name="minute"/>。</summary>
    public static Schedule Daily(int hour, int minute = 0) => new CronSchedule(
        string.Create(CultureInfo.InvariantCulture, $"{minute} {hour} * * *"),
        string.Create(CultureInfo.InvariantCulture, $"每天 {hour:00}:{minute:00}"));

    /// <summary>五欄的 cron 運算式，以台灣時間解讀；<paramref name="description"/> 是給人看的說法（例如「平日 07:30」）。</summary>
    public static Schedule Cron(string expression, string description) => new CronSchedule(expression, description);

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
}

internal sealed record IntervalSchedule : Schedule
{
    private readonly TimeSpan interval;

    public IntervalSchedule(TimeSpan interval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(interval, TimeSpan.FromSeconds(10));
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

    public CronSchedule(string expression, string description)
    {
        // 格式錯誤在定義或設定載入時就拋出，不會等到第一次排程才發現。
        this.expression = CronExpression.Parse(expression);
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
