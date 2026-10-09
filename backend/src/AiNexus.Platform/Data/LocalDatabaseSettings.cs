using System.Globalization;
using Microsoft.Data.SqlClient;

namespace AiNexus.Platform.Data;

public static class LocalDatabaseSettings
{
    public static string Diagnose(Exception exception)
    {
        var sql = exception as SqlException;
        if (exception.ToString().Contains("certificate", StringComparison.OrdinalIgnoreCase) || exception.ToString().Contains("憑證", StringComparison.Ordinal)) return "SQL TLS 憑證驗證失敗。請確認憑證與 DNS 名稱；使用內部自簽憑證時可明確設定 Database.TrustServerCertificate=true，連線仍加密。";
        return sql?.Number switch {
            18456 => "SQL 帳號或密碼不正確，或 SQL 帳密登入未啟用。",
            4060 => "SQL 登入成功，但無法開啟指定資料庫。請初始化 AiNexus 或確認權限。",
            262 or 229 or 916 => "SQL 帳號缺少建庫／DDL 或資料讀寫權限。請由 DBA 使用管理帳號初始化。",
            _ => $"SQL 連線／初始化失敗（{exception.GetType().Name}，代碼 {sql?.Number.ToString(CultureInfo.InvariantCulture) ?? "unknown"}）。請確認伺服器、instance、TCP 連線與權限。"
        };
    }
    public static void Apply(ConfigurationManager configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("Nexus")))
        {
            var configured = new SqlConnectionStringBuilder(configuration.GetConnectionString("Nexus"));
            if (configured.Encrypt == SqlConnectionEncryptOption.Optional)
                throw new InvalidOperationException("SQL connections require Encrypt=True or Strict. TrustServerCertificate=True is supported with encrypted connections.");
            return;
        }
        var user = configuration["Database:User"];
        var password = configuration["Database:Password"];
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrEmpty(password)) return;
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = configuration["Database:Server"] ?? "localhost",
            InitialCatalog = configuration["Database:Name"] ?? "AiNexus",
            UserID = user, Password = password,
            Encrypt = true,
            TrustServerCertificate = configuration.GetValue<bool>("Database:TrustServerCertificate"),
            ConnectTimeout = configuration.GetValue("Database:ConnectTimeoutSeconds", 10),
            PersistSecurityInfo = false
        };
        configuration["ConnectionStrings:Nexus"] = connection.ConnectionString;
    }
}
