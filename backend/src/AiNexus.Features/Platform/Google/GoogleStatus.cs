namespace AiNexus.Features.Platform.Google;

/// <summary>
/// 一次 Google API 呼叫的結果分類，呼叫端依它決定接手、重建、退避或停止，不必自己解讀 HTTP 狀態碼與錯誤原因。
/// 分成兩類：與這一筆資料有關的（<see cref="NotFound"/>、<see cref="Gone"/>、<see cref="Conflict"/>、<see cref="Invalid"/>），
/// 以及與整個服務或設定有關、換下一筆也一樣會失敗的（其餘失敗）。
/// </summary>
public enum GoogleStatus
{
    Ok,

    /// <summary>404：資源不存在（或服務帳戶看不到它）。</summary>
    NotFound,

    /// <summary>410：資源已刪除。</summary>
    Gone,

    /// <summary>409：ID 已被使用（含已刪除、仍保留 ID 的資源）。</summary>
    Conflict,

    /// <summary>400：請求內容不被接受，重試也一樣。</summary>
    Invalid,

    /// <summary>429，或 403 且原因是配額／速率限制；依 <see cref="GoogleResponse{T}.RetryAfter"/> 退避。</summary>
    RateLimited,

    /// <summary>403：服務帳戶沒有權限（例如日曆沒有分享給它）。</summary>
    Forbidden,

    /// <summary>401，或換發權杖被拒：金鑰錯誤、已撤銷，或全網域委派沒有授權這個範圍。</summary>
    CredentialRejected,

    /// <summary>沒有設定服務帳戶金鑰檔，或金鑰檔讀不到、格式錯誤。</summary>
    NotConfigured,

    /// <summary>5xx、逾時或網路錯誤：暫時性，稍後重試。</summary>
    Unavailable
}

/// <summary>Google API 呼叫的結果；<see cref="Message"/> 是 Google 回的錯誤說明或例外類型，不含權杖與金鑰。</summary>
public readonly record struct GoogleResponse<T>(GoogleStatus Status, T? Value = default, string? Message = null, TimeSpan? RetryAfter = null)
{
    public bool IsOk => Status == GoogleStatus.Ok;

    public static GoogleResponse<T> Ok(T value) => new(GoogleStatus.Ok, value);

    /// <summary>把失敗轉成另一個值型別的回應（成功不能轉，因為沒有值）。</summary>
    public GoogleResponse<TOther> As<TOther>() => IsOk ? throw new InvalidOperationException("A successful response has a value to convert.") : new(Status, default, Message, RetryAfter);
}
