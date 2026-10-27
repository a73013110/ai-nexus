using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Admin.Operations;

/// <summary>平台營運模組的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class OperationsAudit
{
    public static readonly AuditAction UsageRead = AuditAction.Access("operations.usage.read", "檢視平台用量");
    public static readonly AuditAction UserUsageRead = AuditAction.Access("identity.user_usage.read", "檢視使用者用量");
}
