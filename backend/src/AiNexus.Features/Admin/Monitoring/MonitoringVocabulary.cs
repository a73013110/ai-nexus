using AiNexus.Features.Admin.AccessControl;

namespace AiNexus.Features.Admin.Monitoring;

/// <summary>固定詞彙表，避免客戶端 URL、資源標題與查詢字串進入監控資料。</summary>
/// <remarks>
/// 監控區域是目錄的頁面或閱讀器 ID（<see cref="FeatureCatalog"/>，前端 presence 送出目前路由的 ID），另有不在目錄的個人設定，
/// 以及無法歸類的 API 用的「系統」。名稱取目錄預設名稱：每個請求都會用到，不查 <c>accesscontrol.Features</c>，管理員改的名稱也不影響它。
/// </remarks>
public static class MonitoringVocabulary
{
    private const string Settings = "settings", System = "system";

    public static bool ValidFeature(string feature) => feature == Settings || FeatureCatalog.Find(feature) is { Kind: not FeatureKind.Action };

    public static string Name(string feature) => feature == Settings ? "個人設定" : FeatureCatalog.Find(feature)?.Name ?? "系統";

    /// <summary>API 路由樣板所屬的頁面：<c>/api/v1/admin/...</c> 依呼叫它的頁面對應，其他依第一段資源名稱。</summary>
    public static string Feature(string route)
    {
        var segments = route.Replace("/api/v1/", "", StringComparison.Ordinal).TrimStart('/').Split('/');
        var next = segments.Length > 1 ? segments[1] : "";
        if (segments[0] == "admin") return Admin(next, segments.Length > 2 ? segments[2] : "");
        return segments[0] switch
        {
            "quality" when next == "retrieval-evals" => FeatureCatalog.QualityRetrieval,
            "conversations" or "runs" or "context" or "models" or "prompt-templates" or "tools" => FeatureCatalog.Chat,
            "attachments" => FeatureCatalog.Files, "documents" => FeatureCatalog.Knowledge, "jobs" => FeatureCatalog.Jobs, "shares" => FeatureCatalog.Sharing,
            "preferences" or "settings" or "me" => Settings,
            // 其餘資源名稱與單頁模組同名（projects、artifacts、knowledge…）；不是頁面的（例如 status）歸「系統」。
            var segment => FeatureCatalog.Find(segment) is { Kind: FeatureKind.Page } ? segment : System,
        };
    }

    // 管理 API 的路徑沿用舊的 /admin/<資源>，授權卻已拆到各頁；這裡依端點的 policy 歸到同一頁，與授權的對應保持一致。
    private static string Admin(string resource, string next) => resource switch
    {
        "monitoring" => FeatureCatalog.Monitoring,
        "audit" => FeatureCatalog.Audit,
        "logs" => FeatureCatalog.DiagnosticsLogs,
        "usage" => FeatureCatalog.OperationsUsage,
        "knowledge" => FeatureCatalog.KnowledgeRetrieval,
        "billing" when next is "prices" or "targets" => FeatureCatalog.BillingPrices,
        "billing" => FeatureCatalog.OperationsUsage,
        "roles" => FeatureCatalog.AccessControlRoles,
        "groups" => FeatureCatalog.AccessControlGroups,
        "features" => FeatureCatalog.AccessControlFeatures,
        "navigation-items" => FeatureCatalog.AccessControlNavigation,
        // 使用者、對話讀取與共用的管理目錄都從使用者頁開始。
        _ => FeatureCatalog.IdentityUsers,
    };

    public static string Action(string method, string feature) => method switch
    {
        "POST" => "執行" + Name(feature), "PUT" or "PATCH" => "更新" + Name(feature), "DELETE" => "刪除" + Name(feature),
        _ => "讀取" + Name(feature)
    };
}
