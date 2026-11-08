namespace AiNexus.Platform.Scheduling;

/// <summary>
/// 排程任務的宣告。<paramref name="Key"/> 是穩定識別（小寫、以點分隔模組與動作，例如 <c>mail.retention</c>），
/// <paramref name="Category"/> 是管理頁分組用的模組名稱（例如「郵件」）。
/// 排程、逾時與開關只寫在設定 <c>Scheduling:Tasks:&lt;Key&gt;</c>（appsettings.json 列出每個任務），程式不另宣告預設值；缺漏時啟動失敗。
/// </summary>
public sealed record ScheduledTaskDefinition(string Key, string Name, string Category, string Description)
{
    /// <summary>
    /// 由任務性質決定、設定不能改的排程（目前只有 <see cref="Schedule.AtStartup"/>）；null 表示排程寫在設定的 <c>Schedule</c>。
    /// </summary>
    public Schedule? FixedSchedule { get; init; }

    /// <summary>連續失敗幾次才通知管理員；偶發的連線中斷下一輪通常就好了，不必每次都吵。</summary>
    public int NotifyAfterFailures { get; init; } = 3;

    /// <summary>
    /// 目前不能執行的原因（例如功能設定沒有打開），可執行時回傳 null。每次排程前都會呼叫，所以只讀設定或記憶體，不查資料庫。
    /// 不能執行的任務不依排程跑，也不接受手動執行；管理頁顯示這個原因。
    /// </summary>
    public Func<IServiceProvider, string?>? Unavailable { get; init; }
}
