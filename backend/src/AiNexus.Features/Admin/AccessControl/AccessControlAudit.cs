using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Admin.AccessControl;

/// <summary>存取控制模組的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class AccessControlAudit
{
    public static readonly AuditAction RoleUpdated = AuditAction.Administration("access-control.role.updated", "調整角色");
    public static readonly AuditAction GroupUpdated = AuditAction.Administration("access-control.group.updated", "調整功能群組與模型政策");
    public static readonly AuditAction FeatureUpdated = AuditAction.Administration("access-control.feature.updated", "調整功能");
    public static readonly AuditAction NavigationRead = AuditAction.Access("access-control.navigation_item.read", "查詢選單");
    public static readonly AuditAction NavigationCreated = AuditAction.Administration("access-control.navigation_item.created", "新增選單項目");
    public static readonly AuditAction NavigationUpdated = AuditAction.Administration("access-control.navigation_item.updated", "修改選單項目");
    public static readonly AuditAction NavigationDeleted = AuditAction.Administration("access-control.navigation_item.deleted", "刪除選單項目");
    public static readonly AuditAction NavigationReordered = AuditAction.Administration("access-control.navigation_item.reordered", "調整選單順序");
}
