using Microsoft.Data.SqlClient;

namespace AiNexus.Database;

public static class LocalDatabaseSettings
{
    public static string Diagnose(Exception exception)
    {
        var sql = exception as SqlException;
        if (exception.ToString().Contains("certificate", StringComparison.OrdinalIgnoreCase) || exception.ToString().Contains("憑證", StringComparison.Ordinal)) return "SQL TLS 憑證驗證失敗。請使用受信任的 SQL 憑證與正確 DNS 名稱；本機開發的自簽憑證可明確設定 Database.TrustServerCertificate。";
        return sql?.Number switch {
            18456 => "SQL 帳號或密碼不正確，或 SQL 帳密登入未啟用。",
            4060 => "SQL 登入成功，但無法開啟指定資料庫。請初始化 AiNexus 或確認權限。",
            262 or 229 or 916 => "SQL 帳號缺少建庫／DDL 或資料讀寫權限。請由 DBA 使用管理帳號初始化。",
            _ => $"SQL 連線／初始化失敗（{exception.GetType().Name}，代碼 {sql?.Number.ToString() ?? "unknown"}）。請確認伺服器、instance、TCP 連線與權限。"
        };
    }
    public static void Apply(ConfigurationManager configuration, bool allowUntrustedCertificates = false)
    {
        if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("Nexus")))
        {
            var configured = new SqlConnectionStringBuilder(configuration.GetConnectionString("Nexus"));
            if (!allowUntrustedCertificates && (configured.TrustServerCertificate || configured.Encrypt == SqlConnectionEncryptOption.Optional)) throw new InvalidOperationException("Production SQL requires encryption and a trusted certificate.");
            return;
        }
        var user = configuration["Database:User"];
        var password = configuration["Database:Password"];
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrEmpty(password)) return;
        if (configuration.GetValue<bool>("Database:TrustServerCertificate") && !allowUntrustedCertificates) throw new InvalidOperationException("Production SQL requires a trusted certificate.");
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = configuration["Database:Server"] ?? "localhost",
            InitialCatalog = configuration["Database:Name"] ?? "AiNexus",
            UserID = user, Password = password,
            Encrypt = true,
            TrustServerCertificate = configuration.GetValue<bool>("Database:TrustServerCertificate"),
            ConnectTimeout = 5,
            PersistSecurityInfo = false
        };
        configuration["ConnectionStrings:Nexus"] = connection.ConnectionString;
    }
}
