using AiNexus.Features.Admin.AccessControl;

namespace AiNexus.Features.Admin.Monitoring;

/// <summary>固定詞彙表，避免客戶端 URL、資源標題與查詢字串進入監控資料。</summary>
/// <remarks>
/// 監控區域大多就是功能 id（引用 <see cref="FeatureIds"/>），另有閱讀器、日誌、個人設定這些不是功能的區域。
/// 名稱是監控專用的固定標籤：每個請求都會用到，不查 <c>accesscontrol.Features</c>，管理員改的功能名稱也不影響它。
/// </remarks>
public static class MonitoringVocabulary
{
    private const string Reader = "reader", Logs = "logs", Settings = "settings";

    private static readonly IReadOnlyDictionary<string, string> Features = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [FeatureIds.Dashboard] = "總覽", [FeatureIds.Chat] = "對話", [FeatureIds.Files] = "檔案庫", [FeatureIds.Knowledge] = "知識庫", [Reader] = "閱讀器",
        [FeatureIds.Projects] = "專案", [FeatureIds.Artifacts] = "成果文件", [FeatureIds.Shared] = "分享", [FeatureIds.Quality] = "品質評測",
        [FeatureIds.Tasks] = "背景任務", [FeatureIds.Repositories] = "程式庫", [FeatureIds.Integrations] = "資料來源", [FeatureIds.Admin] = "存取管理",
        [FeatureIds.Operations] = "平台營運", [FeatureIds.Monitoring] = "即時監控", [FeatureIds.Audit] = "活動稽核", [Logs] = "系統日誌", [Settings] = "個人設定"
    };
    public static bool ValidFeature(string feature) => Features.ContainsKey(feature);
    public static string Name(string feature) => Features.GetValueOrDefault(feature, "系統");
    public static string Feature(string route)
    {
        var segments = route.Replace("/api/v1/", "", StringComparison.Ordinal).TrimStart('/').Split('/');
        var segment = segments[0];
        if (segment == "admin" && segments.Length > 1 && segments[1] is FeatureIds.Monitoring or FeatureIds.Audit or Logs) return segments[1];
        // 平台營運 API 仍使用 /admin 路徑（用量、檢索管理與價格），歸入「平台營運」而非即時監控。
        if (segment == "admin" && segments.Length > 1 && segments[1] is "usage" or "knowledge" or "billing") return FeatureIds.Operations;
        return segment switch
        {
            "conversations" or "runs" or "context" or "models" or "prompt-templates" or "tools" => FeatureIds.Chat,
            "attachments" => FeatureIds.Files, "documents" => FeatureIds.Knowledge, "jobs" => FeatureIds.Tasks, "shares" => FeatureIds.Shared,
            "preferences" or "settings" or "me" => Settings, _ => ValidFeature(segment) ? segment : "system"
        };
    }
    public static string Action(string method, string feature) => method switch
    {
        "POST" => "執行" + Name(feature), "PUT" or "PATCH" => "更新" + Name(feature), "DELETE" => "刪除" + Name(feature),
        _ => "讀取" + Name(feature)
    };
}
