namespace AiNexus.Platform.Time;

/// <summary>
/// 台灣本地時間與 <see cref="DateTimeOffset"/> 的轉換，全系統共用：外部系統（公文系統 GD、打卡機）的時間沒有時區，業務規則的日期與時刻也以台灣為準。
/// 台灣不實施日光節約時間，固定位移就夠，也不依賴伺服器的時區設定。
/// </summary>
public static class TaipeiTime
{
    /// <summary>台灣相對 UTC 的固定位移。</summary>
    public static readonly TimeSpan Offset = TimeSpan.FromHours(8);

    /// <summary>把沒有時區的台灣本地時間轉成 +08:00；<see cref="DateTime.Kind"/> 一律忽略（<c>Local</c> 搭配非本機位移會讓建構子拋例外）。</summary>
    public static DateTimeOffset At(DateTime local) => new(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Offset);

    /// <summary>某一天的台灣本地時刻，例如表定上班。</summary>
    public static DateTimeOffset At(DateOnly date, TimeOnly time) => new(date.ToDateTime(time), Offset);

    /// <summary>同一時刻換成 +08:00 表示；輸入是別的位移（例如 UTC）時，日期與時刻才比得上以台灣為準的規則。</summary>
    public static DateTimeOffset ToTaipei(this DateTimeOffset value) => value.ToOffset(Offset);

    /// <summary>台灣的今天：伺服器時區不一定是台灣，由 <paramref name="clock"/> 的 UTC 換成 +08:00 再取日期。</summary>
    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().ToTaipei().DateTime);
}
