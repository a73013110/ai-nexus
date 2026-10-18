using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.AccessControl;

/// <summary>所有模組共用的存取控制錯誤，以及存取管理（角色、功能、選單群組）的錯誤。</summary>
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
    public static readonly Error InvalidFeatureIcon = Error.Invalid("invalid_feature_icon");
    public static readonly Error NavigationGroupNotFound = Error.NotFound("navigation_group_not_found");
    public static readonly Error NavigationGroupExists = Error.Conflict("navigation_group_exists");
    public static readonly Error NavigationGroupNotEmpty = Error.Conflict("navigation_group_not_empty");
    public static readonly Error NavigationGroupCycle = Error.Invalid("navigation_group_cycle");
}
