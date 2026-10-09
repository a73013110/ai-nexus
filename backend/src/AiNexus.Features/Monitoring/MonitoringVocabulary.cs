namespace AiNexus.Features.Monitoring;

/// <summary>A fixed vocabulary prevents client URLs, resource titles and query strings entering telemetry.</summary>
public static class MonitoringVocabulary
{
    private static readonly IReadOnlyDictionary<string, string> Features = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["dashboard"] = "總覽", ["chat"] = "對話", ["files"] = "檔案庫", ["knowledge"] = "知識庫", ["reader"] = "閱讀器",
        ["projects"] = "專案", ["artifacts"] = "成果文件", ["shared"] = "分享", ["quality"] = "品質評測", ["tasks"] = "背景任務",
        ["repositories"] = "程式庫", ["integrations"] = "資料來源", ["admin"] = "平台管理", ["monitoring"] = "即時監控",
        ["audit"] = "活動稽核", ["logs"] = "系統日誌", ["settings"] = "個人設定"
    };
    public static bool ValidFeature(string feature) => Features.ContainsKey(feature);
    public static string Name(string feature) => Features.GetValueOrDefault(feature, "系統");
    public static string Feature(string route)
    {
        var segments = route.Replace("/api/v1/", "", StringComparison.Ordinal).TrimStart('/').Split('/');
        var segment = segments[0];
        if (segment == "admin" && segments.Length > 1 && segments[1] is "monitoring" or "audit" or "logs") return segments[1];
        return segment switch
        {
            "conversations" or "runs" or "context" or "models" or "prompt-templates" or "tools" => "chat",
            "attachments" => "files", "documents" => "knowledge", "jobs" => "tasks", "shares" => "shared",
            "preferences" or "settings" or "me" => "settings", _ => ValidFeature(segment) ? segment : "system"
        };
    }
    public static string Action(string method, string feature) => method switch
    {
        "POST" => "執行" + Name(feature), "PUT" or "PATCH" => "更新" + Name(feature), "DELETE" => "刪除" + Name(feature),
        _ => "讀取" + Name(feature)
    };
}
