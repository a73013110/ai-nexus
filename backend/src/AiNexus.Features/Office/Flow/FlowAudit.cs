using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Office.Flow;

/// <summary>簽核的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。送出申請由業務模組自己的端點稽核。</summary>
public static class FlowAudit
{
    public static readonly AuditAction InstanceRead = AuditAction.Access("flow.instance.read", "查詢簽核申請");
    public static readonly AuditAction InstanceReviewed = AuditAction.Activity("flow.instance.reviewed", "審核申請");
    public static readonly AuditAction InstanceCancelled = AuditAction.Activity("flow.instance.cancelled", "取消申請");
    public static readonly AuditAction DefinitionRead = AuditAction.Access("flow.definition.read", "查詢簽核流程");
    public static readonly AuditAction DefinitionUpdated = AuditAction.Administration("flow.definition.updated", "修改簽核流程");
}
