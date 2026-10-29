using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Diagnostics;

namespace AiNexus.Features.Platform.Notifications;

/// <summary>通知的顯示規則與型別化目標；排入、通知中心與總覽共用，新增目標只改這裡（另見前端 notification-target.ts）。</summary>
internal static class NotificationDisplay
{
    public const string Error = "error";

    // 失敗只用固定標題與審過的問題訊息，絕不把儲存的原始錯誤文字給使用者看。
    public static string Title(string severity, string title) => severity == Error ? "操作未完成" : title;

    public static string Body(string severity, string body, string? issueCode) => severity == Error ? Issues.Message(issueCode) : body;

    /// <summary>目標種類對應的前端位置；網址取目錄，模組換區時跟著變。未登錄的種類回傳 null。</summary>
    public static string? Link(string kind, Guid id) => kind switch
    {
        "conversation" => FeatureCatalog.RouteOf(FeatureCatalog.Chat) + "/" + id,
        "share" => FeatureCatalog.RouteOf(FeatureCatalog.Sharing) + "/" + id,
        "task" => FeatureCatalog.RouteOf(FeatureCatalog.Jobs) + "?job=" + id,
        "repository-review" => FeatureCatalog.RouteOf(FeatureCatalog.Repositories) + "?review=" + id,
        // 結構異動是整個資料庫的狀態，沒有單一資料列可指，id 不使用。
        "schema-changes" => FeatureCatalog.RouteOf(FeatureCatalog.Releases),
        _ => null,
    };

    /// <summary>只接受有跳轉位置的目標，排入時的白名單與跳轉表因此不會分歧。</summary>
    public static bool IsTarget(string kind) => Link(kind, Guid.Empty) is not null;
}
