using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Work.Sharing;

/// <summary>分享的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class SharingAudit
{
    public static readonly AuditAction ShareRead = AuditAction.Access("sharing.share.read", "查詢分享");
    public static readonly AuditAction ShareCreated = AuditAction.Activity("sharing.share.created", "建立分享");
    public static readonly AuditAction ShareRevoked = AuditAction.Activity("sharing.share.revoked", "撤銷分享");
    public static readonly AuditAction SharedContentRead = AuditAction.Access("sharing.shared_content.read", "開啟分享內容");
    public static readonly AuditAction SharedFileRead = AuditAction.Access("sharing.shared_file.read", "檢視分享的檔案");
}
