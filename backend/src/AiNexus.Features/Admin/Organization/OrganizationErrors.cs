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

    /// <summary>首長單位要有上層，層級 1～9（ADR 0024）。</summary>
    public static readonly Error HeadRankInvalid = Error.Invalid("organization_head_rank_invalid");

    /// <summary>同一個上層已有相同層級的首長單位。</summary>
    public static readonly Error HeadRankDuplicate = Error.Conflict("organization_head_rank_duplicate");

    /// <summary>首長單位底下不能再有首長單位：管轄不連鎖。</summary>
    public static readonly Error HeadUnitNested = Error.Invalid("organization_head_unit_nested");

    /// <summary>單位由首長單位帶領，主管由首長單位的主管擔任，不能再勾選主管。</summary>
    public static readonly Error ManagerByHeadUnit = Error.Conflict("organization_manager_by_head_unit");
    /// <summary>排序清單不是該層現有的全部單位：畫面過期，期間有人新增、搬走或刪除了單位。</summary>
    public static readonly Error OrderStale = Error.Conflict("organization_order_stale");
    /// <summary>不存在或已刪除的使用者不能加入單位。</summary>
    public static readonly Error UserNotFound = Error.NotFound("organization_user_not_found");

    /// <summary>沒有設定公文系統的連線（<c>Databases:Gd</c>），無法匯入；要改設定，不會自己恢復。</summary>
    public static readonly Error ImportNotConfigured = Error.Unavailable("organization_import_not_configured");

    /// <summary>公文系統連不上或逾時。</summary>
    public static readonly Error ImportUnavailable = Error.Unavailable("organization_import_unavailable");

    /// <summary>公文系統拒絕查詢：部門或帳號表不存在、欄位改了，或 GD 帳號沒有讀取權限（docs/features/attendance-gd.md）。</summary>
    public static readonly Error ImportRejected = Error.Unavailable("organization_import_rejected");

    /// <summary>公文系統一個部門都沒有；照算會停用所有已匯入的單位，多半是連錯資料庫，所以拒絕。</summary>
    public static readonly Error ImportEmpty = Error.Conflict("organization_import_empty");

    /// <summary>重算的結果與預覽不同：預覽之後 GD 或組織有人改過，重新預覽後再套用。</summary>
    public static readonly Error ImportStale = Error.Conflict("organization_import_stale");
}
