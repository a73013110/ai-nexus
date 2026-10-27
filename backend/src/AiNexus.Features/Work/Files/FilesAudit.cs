using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Work.Files;

/// <summary>檔案庫的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class FilesAudit
{
    public static readonly AuditAction FileRead = AuditAction.Access("files.file.read", "檢視檔案");
    public static readonly AuditAction FileDownloaded = AuditAction.Access("files.file.downloaded", "下載檔案");
    public static readonly AuditAction FileUploaded = AuditAction.Activity("files.file.uploaded", "上傳檔案");
    public static readonly AuditAction FileRenamed = AuditAction.Activity("files.file.renamed", "重新命名檔案");
    public static readonly AuditAction FileRetained = AuditAction.Activity("files.file.retained", "保留檔案到檔案庫");
    public static readonly AuditAction FileDeleted = AuditAction.Activity("files.file.deleted", "刪除檔案");
    public static readonly AuditAction StorageLimitUpdated = AuditAction.Administration("identity.storage_limit.updated", "調整使用者容量上限");
}
