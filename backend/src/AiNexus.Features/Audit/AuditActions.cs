namespace AiNexus.Features.Audit;

/// <summary>An action filter: a stored action or an action prefix, with the categories its records fall in (its own first).</summary>
public sealed record AuditActionDto(string Value, string Label, IReadOnlyList<string> Categories);

/// <summary>The action filters the audit page offers, in menu order. Categories come from <see cref="AuditCategories"/>.</summary>
public static class AuditActions
{
    public static readonly IReadOnlyList<AuditActionDto> All = new (string Value, string Label)[]
    {
        ("admin.", "平台管理"),
        ("identity.login", "登入"),
        ("identity.logout", "登出"),
        ("logs.", "日誌查閱／匯出"),
        (ExportActivityAudit.Action, "稽核匯出"),
        ("admin.user", "使用者設定"),
        ("identity.test_", "測試身分"),
        ("admin.user_roles", "使用者角色"),
        ("admin.user_model_policy", "個人模型政策"),
        ("admin.user_storage", "使用者容量上限"),
        ("admin.role", "角色授權"),
        ("admin.group", "群組與模型政策"),
        ("admin.feature", "功能異動"),
        ("admin.conversation_read", "對話內容檢視"),
        ("admin.user_usage_read", "使用者用量檢視"),
        ("project.", "專案與範本"),
        ("conversation.", "對話管理"),
        ("artifact.", "成果文件"),
        ("file.", "檔案庫"),
        ("share.", "分享"),
        ("repository.", "程式庫"),
        ("web.search", "網路搜尋"),
        ("knowledge.", "知識庫"),
        ("document.", "文件與索引"),
        ("evaluation.", "評測資源"),
        ("quality.", "評測與回饋"),
        ("run.", "AI 生成"),
        ("integration.", "外部資料查閱"),
        ("resource.acl", "資源授權"),
    }.Select(x => new AuditActionDto(x.Value, x.Label, AuditCategories.Covering(x.Value))).ToArray();
}
