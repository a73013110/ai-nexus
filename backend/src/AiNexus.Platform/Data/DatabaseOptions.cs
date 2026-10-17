using System.ComponentModel.DataAnnotations;
using Microsoft.Data.SqlClient;

namespace AiNexus.Platform.Data;

/// <summary>
/// 位於 <c>Databases:&lt;Name&gt;</c> 區段下的單一 SQL Server 連線設定。
/// <see cref="ConnectionString"/> 是全系統唯一組裝連線字串的地方，統一套用安全原則：強制加密、不保留認證
/// （Persist Security Info=false），設定 <see cref="ReadOnly"/> 時附加 ApplicationIntent=ReadOnly。
/// </summary>
public sealed class DatabaseOptions
{
    public const int MinConnectTimeoutSeconds = 1;
    public const int MaxConnectTimeoutSeconds = 120;

    /// <summary>伺服器位址，格式為 <c>主機</c> 或 <c>主機\執行個體</c>；非預設埠時以 <c>,port</c> 指定。</summary>
    public string Server { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>應放在機密設定檔，與 <see cref="Password"/> 搭配。使用 <see cref="IntegratedSecurity"/> 時保持空白。</summary>
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    /// <summary>以程序身分登入（如 IIS 應用程式集區、gMSA），而不是 SQL 帳密。</summary>
    public bool IntegratedSecurity { get; set; }
    /// <summary>只略過伺服器憑證鏈的驗證，連線仍全程加密。</summary>
    public bool TrustServerCertificate { get; set; }
    public int ConnectTimeoutSeconds { get; set; } = 10;
    /// <summary>連線字串會附加 <c>ApplicationIntent=ReadOnly</c>，但它無法取代 SQL Server 端的唯讀權限設定。</summary>
    public bool ReadOnly { get; set; }

    /// <summary>檢查伺服器、資料庫名稱，以及登入方式（整合式身分或帳密）是否設定完整。</summary>
    public bool Configured => !string.IsNullOrWhiteSpace(Server) && !string.IsNullOrWhiteSpace(Name)
        && (IntegratedSecurity || !string.IsNullOrWhiteSpace(User) && !string.IsNullOrEmpty(Password));

    /// <param name="catalog">同一伺服器上要改連的資料庫（如 <c>master</c>）；為 null 時使用 <see cref="Name"/>。</param>
    /// <returns>連線字串；設定未完整（未通過 <see cref="Configured"/>）時回傳空字串。</returns>
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

    /// <param name="key">此項目的設定鍵，讓每則驗證錯誤都能精確指出要修正的路徑。</param>
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
