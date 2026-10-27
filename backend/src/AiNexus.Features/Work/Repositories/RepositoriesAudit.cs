using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Work.Repositories;

/// <summary>程式庫的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class RepositoriesAudit
{
    public static readonly AuditAction RepositoryRead = AuditAction.Access("repositories.repository.read", "瀏覽程式庫");
    public static readonly AuditAction FileRead = AuditAction.Access("repositories.file.read", "檢視程式庫檔案");
    public static readonly AuditAction CommitRead = AuditAction.Access("repositories.commit.read", "檢視程式庫提交與差異");
    public static readonly AuditAction IssueRead = AuditAction.Access("repositories.issue.read", "檢視程式庫議題");
    public static readonly AuditAction PullRequestRead = AuditAction.Access("repositories.pull_request.read", "檢視合併請求");
    public static readonly AuditAction RepositoryPinned = AuditAction.Activity("repositories.repository.pinned", "釘選程式庫");
    public static readonly AuditAction RepositoryUnpinned = AuditAction.Activity("repositories.repository.unpinned", "取消釘選程式庫");
    public static readonly AuditAction ConnectionConnected = AuditAction.Activity("repositories.connection.connected", "連結程式庫");
    public static readonly AuditAction ConnectionDisconnected = AuditAction.Activity("repositories.connection.disconnected", "中斷程式庫連結");
    public static readonly AuditAction FolderImported = AuditAction.Activity("repositories.folder.imported", "匯入程式庫資料夾到知識庫");
    public static readonly AuditAction ConversationCreated = AuditAction.Activity("repositories.conversation.created", "從程式庫開始對話");
    public static readonly AuditAction ReviewRead = AuditAction.Access("repositories.review.read", "查詢程式碼檢查");
    public static readonly AuditAction ReviewCreated = AuditAction.Activity("repositories.review.created", "建立程式碼檢查");
    public static readonly AuditAction ReviewCancelled = AuditAction.Activity("repositories.review.cancelled", "停止程式碼檢查");
    public static readonly AuditAction ReviewRetried = AuditAction.Activity("repositories.review.retried", "重試程式碼檢查");
    public static readonly AuditAction ReviewCommented = AuditAction.Activity("repositories.review.commented", "回貼程式碼檢查意見");
}
