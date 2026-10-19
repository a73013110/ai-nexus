using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Organization;

/// <summary>組織單位與人員所屬的維護錯誤。</summary>
public static class OrganizationErrors
{
    public static readonly Error UnitNotFound = Error.NotFound("organization_unit_not_found");
    public static readonly Error UnitNotEmpty = Error.Conflict("organization_unit_not_empty");
    public static readonly Error UnitCycle = Error.Invalid("organization_unit_cycle");
    public static readonly Error DepthExceeded = Error.Invalid("organization_depth_exceeded");
    public static readonly Error ParentInvalid = Error.Invalid("organization_parent_invalid");
    public static readonly Error NameDuplicate = Error.Conflict("organization_unit_name_duplicate");
    /// <summary>停用的單位不能加入新成員；既有成員仍可修改或移除。</summary>
    public static readonly Error UnitDisabled = Error.Conflict("organization_unit_disabled");
    public static readonly Error MemberNotFound = Error.NotFound("organization_member_not_found");
    /// <summary>排序清單不是該層現有的全部單位：畫面過期，期間有人新增、搬走或刪除了單位。</summary>
    public static readonly Error OrderStale = Error.Conflict("organization_order_stale");
    /// <summary>不存在或已刪除的使用者不能加入單位。</summary>
    public static readonly Error UserNotFound = Error.NotFound("organization_user_not_found");
}
