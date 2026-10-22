namespace AiNexus.Features.Staff.Attendance;

/// <summary>
/// 出勤的時間都是台灣本地時間：打卡機與公文系統（GD）的時間沒有時區，本系統存成固定 +08:00 的 <see cref="DateTimeOffset"/>（資料庫以 check 限制刷卡時間的位移）。
/// 台灣不實施日光節約時間，固定位移就夠，也不依賴伺服器的時區設定。
/// </summary>
public static class TaipeiTime
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(8);

    /// <summary>把沒有時區的台灣本地時間轉成 +08:00；<see cref="DateTime.Kind"/> 一律忽略（<c>Local</c> 搭配非本機位移會讓建構子拋例外）。</summary>
    public static DateTimeOffset At(DateTime local) => new(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Offset);

    /// <summary>出勤日的某個台灣本地時刻，例如表定上班。</summary>
    public static DateTimeOffset At(DateOnly date, TimeOnly time) => new(date.ToDateTime(time), Offset);

    /// <summary>同一時刻換成 +08:00 表示；呼叫端給了別的位移（例如 UTC）時，日期與時刻才比得上出勤規則。</summary>
    public static DateTimeOffset Of(DateTimeOffset value) => value.ToOffset(Offset);

    /// <summary>台灣的今天：伺服器時區不一定是台灣，由 <paramref name="clock"/> 的 UTC 換成 +08:00 再取日期。</summary>
    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(Of(clock.GetUtcNow()).DateTime);
}
