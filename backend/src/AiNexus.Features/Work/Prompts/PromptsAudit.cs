using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Work.Prompts;

/// <summary>提示範本的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class PromptsAudit
{
    public static readonly AuditAction TemplateCreated = AuditAction.Activity("chat.prompt_template.created", "建立提示範本");
    public static readonly AuditAction TemplateUpdated = AuditAction.Activity("chat.prompt_template.updated", "修改提示範本");
    public static readonly AuditAction TemplateDeleted = AuditAction.Activity("chat.prompt_template.deleted", "刪除提示範本");
}
