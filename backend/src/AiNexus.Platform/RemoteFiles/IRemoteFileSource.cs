namespace AiNexus.Platform.RemoteFiles;

/// <summary>
/// 別的系統上的一個檔案：<see cref="Source"/> 是 <see cref="IRemoteFileSource.Name"/>，<see cref="Reference"/> 是該來源自己認得的位置（例如公文系統的附件路徑）。
/// 只在伺服器端流動：發給瀏覽器的是包住它的短期憑證，對方系統的網址與路徑不會出現在前端。
/// </summary>
public sealed record RemoteFileReference(string Source, string Reference);

/// <summary>讀取遠端檔案的結果；<see cref="Content"/> 只在 <see cref="RemoteFileStatus.Found"/> 時有值。</summary>
public sealed record RemoteFileRead(RemoteFileStatus Status, byte[]? Content = null)
{
    public static RemoteFileRead NotFound { get; } = new(RemoteFileStatus.NotFound);
    public static RemoteFileRead Unavailable { get; } = new(RemoteFileStatus.Unavailable);
    public static RemoteFileRead TooLarge { get; } = new(RemoteFileStatus.TooLarge);
}

public enum RemoteFileStatus
{
    Found,

    /// <summary>對方沒有這個檔案，或位置不合來源的規則。</summary>
    NotFound,

    /// <summary>對方未設定、連不上、逾時或回應異常；稍後可能恢復。</summary>
    Unavailable,

    /// <summary>超過來源設定的大小上限；不會自己恢復。</summary>
    TooLarge,
}

/// <summary>
/// 遠端檔案來源（例如公文系統的附件伺服器），由擁有該系統知識的模組實作並以 <see cref="RemoteFileSources.AddRemoteFileSource{TSource}"/> 註冊。
/// 放在 Platform 是讓系統模組不必依賴發憑證、提供下載的 RemoteFiles 模組。
/// </summary>
public interface IRemoteFileSource
{
    /// <summary>寫進憑證的穩定名稱（例如 <c>gd</c>）；改名會讓已發出的憑證失效。</summary>
    string Name { get; }

    /// <summary>
    /// 讀取整個檔案到記憶體（有大小上限）：要先確認內容像個檔案、沒有超過上限，才開始回應瀏覽器。
    /// <paramref name="reference"/> 來自憑證，憑證有簽章，但實作仍要驗證格式，不能拿它組出任意網址。
    /// </summary>
    Task<RemoteFileRead> ReadAsync(string reference, CancellationToken ct);
}

public static class RemoteFileSources
{
    /// <summary>加入一個遠端檔案來源；<see cref="IRemoteFileSource.Name"/> 不可與其他來源重複。</summary>
    public static IServiceCollection AddRemoteFileSource<TSource>(this IServiceCollection services) where TSource : class, IRemoteFileSource
        => services.AddScoped<IRemoteFileSource, TSource>();
}
