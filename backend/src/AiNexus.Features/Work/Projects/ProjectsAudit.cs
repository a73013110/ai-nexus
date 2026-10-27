using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Work.Projects;

/// <summary>專案的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class ProjectsAudit
{
    public static readonly AuditAction ProjectRead = AuditAction.Access("projects.project.read", "查詢專案");
    public static readonly AuditAction ProjectCreated = AuditAction.Activity("projects.project.created", "建立專案");
    public static readonly AuditAction ProjectUpdated = AuditAction.Activity("projects.project.updated", "修改專案");
    public static readonly AuditAction ProjectDeleted = AuditAction.Activity("projects.project.deleted", "刪除專案");
    public static readonly AuditAction AccessRead = AuditAction.Access("projects.project_access.read", "檢視專案權限");
    public static readonly AuditAction ProjectShared = AuditAction.Activity("projects.project.shared", "設定專案權限");
    public static readonly AuditAction FileRead = AuditAction.Access("projects.project_file.read", "查詢專案檔案");
    public static readonly AuditAction FileCreated = AuditAction.Activity("projects.project_file.created", "加入專案檔案");
    public static readonly AuditAction ArtifactCreated = AuditAction.Activity("projects.project_artifact.created", "在專案建立成果文件");
    public static readonly AuditAction ConversationRead = AuditAction.Access("projects.project_conversation.read", "查詢專案對話");
    public static readonly AuditAction ConversationCreated = AuditAction.Activity("projects.project_conversation.created", "在專案開始對話");
    public static readonly AuditAction ConversationAssigned = AuditAction.Activity("projects.conversation.assigned", "把對話移入專案");
    public static readonly AuditAction ConversationUnassigned = AuditAction.Activity("projects.conversation.unassigned", "把對話移出專案");
    public static readonly AuditAction TemplateRead = AuditAction.Access("projects.project_template.read", "查詢專案範本");
    public static readonly AuditAction TemplateCreated = AuditAction.Activity("projects.project_template.created", "建立專案範本");
    public static readonly AuditAction TemplateUpdated = AuditAction.Activity("projects.project_template.updated", "修改專案範本");
    public static readonly AuditAction TemplateDeleted = AuditAction.Activity("projects.project_template.deleted", "刪除專案範本");
}
