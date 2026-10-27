using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Admin.Organization;

/// <summary>組織模組的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class OrganizationAudit
{
    public static readonly AuditAction UnitRead = AuditAction.Access("organization.unit.read", "查詢組織單位");
    public static readonly AuditAction UnitCreated = AuditAction.Administration("organization.unit.created", "新增組織單位");
    public static readonly AuditAction UnitUpdated = AuditAction.Administration("organization.unit.updated", "修改組織單位");
    public static readonly AuditAction UnitDeleted = AuditAction.Administration("organization.unit.deleted", "刪除組織單位");
    public static readonly AuditAction UnitReordered = AuditAction.Administration("organization.unit.reordered", "調整組織單位順序");
    public static readonly AuditAction UnitImportRead = AuditAction.Access("organization.unit_import.read", "預覽公文系統部門匯入");
    public static readonly AuditAction UnitImported = AuditAction.Administration("organization.unit.imported", "從公文系統匯入部門");
    public static readonly AuditAction MemberRead = AuditAction.Access("organization.unit_member.read", "查詢單位成員");
    public static readonly AuditAction MemberUpdated = AuditAction.Administration("organization.unit_member.updated", "設定單位成員");
    public static readonly AuditAction MemberDeleted = AuditAction.Administration("organization.unit_member.deleted", "移出單位成員");
}
