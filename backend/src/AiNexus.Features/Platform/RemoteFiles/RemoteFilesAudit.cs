using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Platform.RemoteFiles;

/// <summary>遠端檔案的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class RemoteFilesAudit
{
    public static readonly AuditAction FileDownloaded = AuditAction.Access("remote-files.file.downloaded", "下載遠端檔案");
}
