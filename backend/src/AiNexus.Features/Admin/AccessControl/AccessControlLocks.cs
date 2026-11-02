namespace AiNexus.Features.Admin.AccessControl;

/// <summary>存取控制與帳號的行政寫入鎖（<see cref="AuditTarget.Locks"/>）。</summary>
public static class AccessControlLocks
{
    /// <summary>
    /// 所有會改變「誰持有什麼功能」或帳號對應的寫入：帳號、角色、群組、功能、AD 匯入與改綁、首次登入。
    /// 新帳號接在使用者順序最後（<c>NewUserOrder</c>）要讀整份順序，所以排序的寫入也在這把鎖內。
    /// </summary>
    public const string Authorization = "access-control:authorization";

    /// <summary>選單的結構規則（父節點、成環、深度、同層重複）跨整份選單。</summary>
    public const string Navigation = "access-control:navigation";
}
