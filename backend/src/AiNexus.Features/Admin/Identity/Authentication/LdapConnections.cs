using System.DirectoryServices.Protocols;
using System.Net;
using System.Text;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Identity.Authentication;

/// <summary>
/// AD 的 LDAP 連線：登入驗證（<see cref="LdapAuthenticator"/>）與目錄查詢（<c>LdapDirectory</c>）共用同一套
/// 設定檢查、TLS、逾時與服務帳號 bind，任一處調整連線政策時兩邊一起生效。
/// </summary>
public static class LdapConnections
{
    /// <summary>未設定服務帳號密碼時拋出 <c>ad_not_configured</c>；回傳同一份設定方便串接。</summary>
    public static AdAuthenticationOptions Require(AdAuthenticationOptions settings)
    {
        if (!settings.Configured) throw Unavailable("ad_not_configured", "AD 登入尚未完成設定，請在秘密檔設定 BindPassword。");
        return settings;
    }

    /// <summary>
    /// 非 Windows 主機的錯誤（SID 轉換只在 Windows 可用）。呼叫端自己寫 <c>if (!OperatingSystem.IsWindows()) throw …</c>，
    /// 平台分析器才看得出後面的程式只在 Windows 執行。
    /// </summary>
    public static ExternalServiceException PlatformUnavailable() => Unavailable("ad_platform_unavailable", "此部署的 AD 登入需要 Windows 主機。");

    /// <summary>以服務帳號 bind；失敗時釋放連線並拋出 <c>ad_bind_unavailable</c>，不把 LDAP 訊息帶進回應。</summary>
    public static LdapConnection BindService(AdAuthenticationOptions settings)
    {
        var service = Connect(settings, settings.BindName, settings.BindPassword);
        try { service.Bind(); return service; }
        catch (LdapException error) { service.Dispose(); throw Unavailable("ad_bind_unavailable", "AD 服務帳號連線失敗，請確認 Server、BindUser 與 BindPassword。", error); }
    }

    /// <summary>建立尚未 bind 的連線：LDAP v3、不追 referral（避免被導到設定以外的主機），LDAPS 或 StartTLS 二選一，絕不明文。</summary>
    public static LdapConnection Connect(AdAuthenticationOptions settings, string user, string password)
    {
        var identifier = new LdapDirectoryIdentifier(settings.Host, settings.Port);
        var connection = new LdapConnection(identifier, new NetworkCredential(user, password), AuthType.Basic) { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            connection.SessionOptions.ProtocolVersion = 3;
            connection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
            if (settings.UseLdaps) connection.SessionOptions.SecureSocketLayer = true;
            else connection.SessionOptions.StartTransportLayerSecurity(null);
            return connection;
        }
        catch { connection.Dispose(); throw Unavailable("ad_tls_unavailable", "無法建立 AD 加密連線，請確認 StartTLS／LDAPS 與憑證設定。"); }
    }

    /// <summary>依 RFC 4515 跳脫 LDAP 篩選器中的特殊字元，避免帳號名稱或搜尋字被注入成額外的查詢條件。</summary>
    public static string EscapeFilter(string value)
    {
        var escaped = new StringBuilder();
        foreach (var character in value) escaped.Append(character switch { '\\' => "\\5c", '*' => "\\2a", '(' => "\\28", ')' => "\\29", '\0' => "\\00", _ => character.ToString() });
        return escaped.ToString();
    }

    /// <summary>userAccountControl 的 ACCOUNTDISABLE 位元（2）；屬性缺漏或格式不對時視為未停用，由 bind 結果決定能否登入。</summary>
    public static bool Disabled(SearchResultEntry entry)
        => entry.Attributes["userAccountControl"]?[0] is string flags && int.TryParse(flags, out var value) && (value & 2) != 0;

    public static ExternalServiceException Unavailable(string code, string detail, Exception? inner = null) => new(Error.Unavailable(code), detail, inner);
}
