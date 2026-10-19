using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.AccessControl;

/// <summary>所有模組共用的存取控制錯誤，以及存取管理（角色、功能、選單）的錯誤。</summary>
public static class AccessControlErrors
{
    /// <summary>
    /// 帳號缺少該操作所需的功能授權。所有功能共用這一個代碼，不論是 policy 擋下請求，或 handler、交易、背景工作重新檢查授權。
    /// 回應不寫出是哪個功能（一個 policy 可能接受多個功能之一）；需要該功能的頁面本身已知道是哪一個。
    /// </summary>
    public static readonly Error FeatureForbidden = Error.Forbidden("feature_forbidden");

    /// <summary>行政變更會讓執行者失去自己的管理權限（<see cref="AdministrativeAudit"/> 與帳號管理共用）。</summary>
    public static readonly Error Lockout = Error.Conflict("admin_lockout");
    public static readonly Error AdminResourceNotFound = Error.NotFound("admin_resource_not_found");
    public static readonly Error InvalidAccessId = Error.Invalid("invalid_access_id");
    public static readonly Error InvalidAccessIds = Error.Invalid("invalid_access_ids");
    public static readonly Error InvalidAccessName = Error.Invalid("invalid_access_name");
    public static readonly Error UnknownAccessId = Error.Invalid("unknown_access_id");
    public static readonly Error InvalidOrder = Error.Invalid("invalid_order");
    public static readonly Error NavigationItemNotFound = Error.NotFound("navigation_item_not_found");
    public static readonly Error NavigationItemNotEmpty = Error.Conflict("navigation_item_not_empty");
    public static readonly Error NavigationItemCycle = Error.Invalid("navigation_item_cycle");
    public static readonly Error NavigationDepthExceeded = Error.Invalid("navigation_depth_exceeded");
    /// <summary>父節點不存在或不是分群。page 指向不存在或不是頁面的功能、audiences 有不存在的群組，沿用 <see cref="UnknownAccessId"/>。</summary>
    public static readonly Error NavigationParentInvalid = Error.Invalid("navigation_parent_invalid");
    public static readonly Error NavigationPageDuplicate = Error.Conflict("navigation_page_duplicate");
    public static readonly Error NavigationLinkHostNotAllowed = Error.Invalid("navigation_link_host_not_allowed");
    /// <summary>修改時送來的種類與節點現有種類不同。種類不能改，屬於請求形狀錯誤，所以沿用驗證失敗的通用代碼而不另開代碼。</summary>
    public static readonly Error NavigationKindMismatch = Error.Invalid("validation_failed");
}
