namespace AiNexus.Features.Admin.Identity.Users;

/// <summary>Windows 整合驗證時依 SID 查目錄的姓名與信箱；驗證本身不帶這些資料。</summary>
public interface IDirectoryProfileLookup
{
    /// <summary>
    /// 目錄的姓名（沒填時是帳號名，與 LDAP 登入的規則相同）與信箱。查不到或目錄暫時連不上時回 null：
    /// 呼叫端必須把它當成「不知道」，不可拿帳號名去覆寫既有使用者的姓名與信箱。
    /// </summary>
    DirectoryProfile? Find(string sid, string account);
}
