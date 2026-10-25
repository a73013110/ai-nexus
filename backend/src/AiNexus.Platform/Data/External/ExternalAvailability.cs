namespace AiNexus.Platform.Data.External;

/// <summary>外部資料庫存取失敗的原因分類；只有分類，不含例外訊息、連線字串或帳密。</summary>
public enum ExternalFailure
{
    /// <summary>沒有 <c>Databases:&lt;Name&gt;</c> 設定。</summary>
    NotConfigured,

    /// <summary>連不上或其他資料庫錯誤（網路、主機、登入、資料庫離線）：整個不能用。</summary>
    Unavailable,

    /// <summary>連線或查詢逾時，含監控探測超過時限。</summary>
    Timeout,

    /// <summary>連得上，但對方拒絕這個查詢：資料表、欄位不存在或沒有權限（對方改版，或帳號少了授權）。只影響部分查詢。</summary>
    Rejected,
}

/// <summary>查詢的結果：成功時 <paramref name="Rows"/> 有值；失敗時為 null，<paramref name="Failure"/> 是原因分類。</summary>
public sealed record ExternalRead<T>(IReadOnlyList<T>? Rows, ExternalFailure? Failure);

/// <summary>
/// <see cref="ExternalDatabase{TDatabase}"/> 實際存取（含監控探測）的最近結果，監控由它判定健康。只在記憶體，重啟後歸零。
/// </summary>
/// <param name="Outage">目前的連線層級中斷（<see cref="ExternalFailure.Rejected"/> 以外）；成功一次就清除。null 表示可用或還沒存取過。</param>
/// <param name="LastSuccessAt">最近一次成功（查詢或探測）的時間。</param>
/// <param name="LastFailureAt">最近一次失敗的時間，含查詢被拒。</param>
/// <param name="LastFailure">最近一次失敗的原因，含 <see cref="ExternalFailure.Rejected"/>。</param>
/// <param name="LastRejectedAt">最近一次查詢被拒的時間；另外記，因為之後的連線失敗會蓋掉 <paramref name="LastFailure"/>。</param>
public sealed record ExternalAvailability(ExternalFailure? Outage, DateTimeOffset? LastSuccessAt, DateTimeOffset? LastFailureAt, ExternalFailure? LastFailure, DateTimeOffset? LastRejectedAt)
{
    /// <summary>
    /// 查詢被拒後維持 degraded、且不重複記日誌的期間。被拒的查詢只在有人開對應頁面時才會再跑，探測的 <c>SELECT 1</c> 測不出來，
    /// 所以不能等「下一次成功」才恢復，改以最後一次被拒後一段時間沒再發生為準。
    /// </summary>
    public static readonly TimeSpan RejectedWindow = TimeSpan.FromHours(1);

    public static readonly ExternalAvailability Unknown = new(null, null, null, null, null);

    /// <summary>最近一次存取（不論成敗）的時間；還沒存取過為 null。</summary>
    public DateTimeOffset? LastAttemptAt => LastSuccessAt > LastFailureAt || LastFailureAt is null ? LastSuccessAt : LastFailureAt;
}
