using System.ComponentModel.DataAnnotations;
using Microsoft.Data.SqlClient;

namespace AiNexus.Platform.Data;

/// <summary>
/// 位於 <c>Databases:&lt;Name&gt;</c> 區段下的單一 SQL Server 資料庫連線配置。
/// <see cref="ConnectionString"/> 為全系統唯一建置連線字串之處，確保一致的安全性防護原則：
/// 強制要求連線加密、嚴格禁止保留使用者認證（Persist Security Info=false），且在設定 <see cref="ReadOnly"/> 時強制附加 ApplicationIntent=ReadOnly。
/// </summary>
public sealed class DatabaseOptions
{
    public const int MinConnectTimeoutSeconds = 1;
    public const int MaxConnectTimeoutSeconds = 120;

    /// <summary>主機名稱或 <c>主機\執行個體</c>；非預設通訊埠時以 <c>,port</c> 指定。</summary>
    public string Server { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>應存放於機密設定檔中，與 <see cref="Password"/> 搭配使用。使用 <see cref="IntegratedSecurity"/> 時保持為空。</summary>
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    /// <summary>以處理程序識別身分登入（如 IIS 應用程式集區、gMSA），而非使用 SQL 帳密驗證。</summary>
    public bool IntegratedSecurity { get; set; }
    /// <summary>僅跳過伺服器憑證鏈驗證；連線傳輸過程仍強制全程加密。</summary>
    public bool TrustServerCertificate { get; set; }
    public int ConnectTimeoutSeconds { get; set; } = 10;
    /// <summary>連線字串附加 <c>ApplicationIntent=ReadOnly</c>。注意這無法取代 SQL 伺服器端的唯讀權限配置。</summary>
    public bool ReadOnly { get; set; }

    /// <summary>檢查伺服器名稱、資料庫名稱以及完整登入驗證（整合式身分或帳號密碼）是否皆已配置完整。</summary>
    public bool Configured => !string.IsNullOrWhiteSpace(Server) && !string.IsNullOrWhiteSpace(Name)
        && (IntegratedSecurity || !string.IsNullOrWhiteSpace(User) && !string.IsNullOrEmpty(Password));

    /// <param name="catalog">欲連接於同一伺服器上的替換資料庫名稱（例如 <c>master</c>）；為 null 時預設開啟 <see cref="Name"/>。</param>
    /// <returns>連線字串；若資料庫尚未完整配置（未達 <see cref="Configured"/>）則回傳空字串。</returns>
    public string ConnectionString(string? catalog = null)
    {
        if (!Configured) return "";
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = Server, InitialCatalog = catalog ?? Name,
            Encrypt = SqlConnectionEncryptOption.Mandatory, TrustServerCertificate = TrustServerCertificate,
            ConnectTimeout = ConnectTimeoutSeconds, PersistSecurityInfo = false
        };
        if (IntegratedSecurity) connection.IntegratedSecurity = true;
        else { connection.UserID = User; connection.Password = Password; }
        if (ReadOnly) connection.ApplicationIntent = ApplicationIntent.ReadOnly;
        return connection.ConnectionString;
    }

    /// <param name="key">此項目的組態鍵名稱，讓每個驗證錯誤訊息都能精確指出需修正的設定路徑。</param>
    internal IEnumerable<ValidationResult> Validate(string key)
    {
        if (ConnectTimeoutSeconds is < MinConnectTimeoutSeconds or > MaxConnectTimeoutSeconds)
            yield return new($"{key}:{nameof(ConnectTimeoutSeconds)} must be {MinConnectTimeoutSeconds}–{MaxConnectTimeoutSeconds}.", [key]);
        var hasUser = !string.IsNullOrWhiteSpace(User);
        var hasPassword = !string.IsNullOrEmpty(Password);
        if (IntegratedSecurity && (hasUser || hasPassword))
            yield return new($"{key}:{nameof(IntegratedSecurity)}=true signs in as the process identity; leave {key}:{nameof(User)} and {key}:{nameof(Password)} empty.", [key]);
        if (!IntegratedSecurity && hasUser != hasPassword)
            yield return new($"{key}:{(hasUser ? nameof(Password) : nameof(User))} is missing; {nameof(User)} and {nameof(Password)} are set together.", [key]);
        if ((IntegratedSecurity || hasUser || hasPassword) && (string.IsNullOrWhiteSpace(Server) || string.IsNullOrWhiteSpace(Name)))
            yield return new($"{key}:{(string.IsNullOrWhiteSpace(Server) ? nameof(Server) : nameof(Name))} is required once the sign-in is set.", [key]);
    }
}
