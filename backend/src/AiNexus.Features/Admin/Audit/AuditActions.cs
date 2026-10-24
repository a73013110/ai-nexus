namespace AiNexus.Features.Admin.Audit;

/// <summary>一個動作篩選：完整動作或動作前綴，附上它可能查到的紀錄分類（自身分類排第一）。</summary>
public sealed record AuditActionDto(string Value, string Label, IReadOnlyList<string> Categories);

/// <summary>稽核頁的動作選單，依顯示順序排列；分類由 <see cref="AuditCategories"/> 推算，前端不再寫死。</summary>
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
        ("account.", "帳號安全"),
        ("admin.user_roles", "使用者角色"),
        ("admin.user_email", "使用者信箱"),
        ("admin.user_import", "從 AD 匯入使用者"),
        ("admin.user_sync", "從 AD 更新使用者"),
        ("admin.user_ad_", "使用者 AD 綁定"),
        ("admin.user_model_policy", "個人模型政策"),
        ("admin.user_storage", "使用者容量上限"),
        ("admin.role", "角色授權"),
        ("admin.group", "群組與模型政策"),
        ("admin.feature", "功能異動"),
        ("admin.navigation_", "選單異動"),
        ("admin.organization_", "組織異動"),
        ("admin.attendance_", "出勤卡號異動"),
        ("admin.calendar_", "假日設定異動"),
        ("attendance.records.export", "出勤紀錄匯出"),
        ("admin.mail_", "郵件重寄與測試"),
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
