using System.Globalization;
using Microsoft.Data.SqlClient;

namespace AiNexus.Platform.Data;

/// <summary>將 SQL 連線或初始化異常轉化為便於維運人員排除問題的診斷訊息，並遮蔽敏感認證資訊。</summary>
public static class DatabaseDiagnosis
{
    /// <param name="exception">發生的例外狀況。</param>
    /// <param name="database">發生失敗的 <c>Databases</c> 設定名稱，訊息中將明確標示該檢核鍵。</param>
    public static string Describe(Exception exception, string database)
    {
        var sql = exception as SqlException;
        var key = $"{DatabasesOptions.Section}.{database}";
        if (exception.ToString().Contains("certificate", StringComparison.OrdinalIgnoreCase) || exception.ToString().Contains("憑證", StringComparison.Ordinal)) return $"SQL TLS 憑證驗證失敗。請確認憑證與 DNS 名稱；使用內部自簽憑證時可明確設定 {key}.TrustServerCertificate=true，連線仍加密。";
        return sql?.Number switch {
            18456 => $"SQL 帳號或密碼不正確，或 SQL 帳密登入未啟用。請確認 {key}.User／Password；{key}.IntegratedSecurity=true 時確認 application pool 身分的 SQL 登入。",
            4060 => $"SQL 登入成功，但無法開啟 {key}.Name 指定的資料庫。請初始化資料庫或確認權限。",
            262 or 229 or 916 => "SQL 帳號缺少建庫／DDL 或資料讀寫權限。請由 DBA 使用管理帳號初始化。",
            _ => $"SQL 連線／初始化失敗（{exception.GetType().Name}，代碼 {sql?.Number.ToString(CultureInfo.InvariantCulture) ?? "unknown"}）。請確認 {key}.Server、instance、TCP 連線與權限。"
        };
    }
}
