using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Work.Artifacts;

/// <summary>成果文件模組的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class ArtifactsAudit
{
    public static readonly AuditAction ArtifactRead = AuditAction.Access("artifacts.artifact.read", "檢視成果文件");
    public static readonly AuditAction ArtifactCreated = AuditAction.Activity("artifacts.artifact.created", "建立成果文件");
    public static readonly AuditAction ArtifactUpdated = AuditAction.Activity("artifacts.artifact.updated", "修改成果文件");
    public static readonly AuditAction ArtifactDeleted = AuditAction.Activity("artifacts.artifact.deleted", "刪除成果文件");
    public static readonly AuditAction ArtifactExported = AuditAction.Access("artifacts.artifact.exported", "匯出成果文件");
    public static readonly AuditAction VersionRead = AuditAction.Access("artifacts.artifact_version.read", "檢視成果文件版本");
    public static readonly AuditAction AccessRead = AuditAction.Access("artifacts.artifact_access.read", "檢視成果文件權限");
    public static readonly AuditAction ArtifactShared = AuditAction.Activity("artifacts.artifact.shared", "設定成果文件權限");
    public static readonly AuditAction TextTransformed = AuditAction.Activity("artifacts.text.transformed", "以 AI 改寫文字");
}
