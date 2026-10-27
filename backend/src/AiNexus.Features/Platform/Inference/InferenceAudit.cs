using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Platform.Inference;

/// <summary>模型政策的稽核動作（在使用者管理頁操作，所以歸身分模組）（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class InferenceAudit
{
    public static readonly AuditAction UserPolicyRead = AuditAction.Access("identity.model_policy.read", "檢視個人模型政策");
    public static readonly AuditAction UserPolicyUpdated = AuditAction.Administration("identity.model_policy.updated", "設定個人模型政策");
}
