using Microsoft.CodeAnalysis;

namespace AiNexus.Generators.Composition;

/// <summary>組合產生器的診斷，代碼 AINX1xxx（各產生器的代碼區段見 docs/architecture/source-generators.md）。</summary>
internal static class CompositionDiagnostics
{
    private const string Category = "AiNexus.Composition";

    public static readonly DiagnosticDescriptor InvalidTarget = new(
        "AINX1001", "組合目標必須是頂層的 static partial 類別",
        "'{0}' 標了 [FeatureComposition]，但不是頂層的 static partial 類別", Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateTarget = new(
        "AINX1002", "一個組件只能有一個組合目標",
        "'{0}' 標了 [FeatureComposition]，但組件裡已有 '{1}'", Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvisibleType = new(
        "AINX1003", "組合的型別必須在組件內可見",
        "'{0}' 是{1}，但它或外層型別不是 public／internal，產生的註冊程式碼無法引用", Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    // 要看完整個組件才知道有沒有人掛載，屬於編譯結束時的診斷：IDE 只在完整方案分析時即時顯示，dotnet build 一定會檢查。
    public static readonly DiagnosticDescriptor UnmappedSlice = new(
        "AINX1004", "端點切片必須掛載",
        "'{0}' 是端點切片，但組件裡沒有任何 MapSlice<{0}>()，端點不會存在；在模組的 MapEndpoints 以 group.MapSlice<T>() 掛上它", Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true, customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor DuplicateValidator = new(
        "AINX1005", "同一個 request 型別只能有一個請求驗證器",
        "'{0}' 驗證 '{1}'，但 '{2}' 也驗證它；過濾器依 request 型別只能套用一個（啟動時也會失敗），把規則合併到同一個驗證器", Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidService = new(
        "AINX1006", "標記的服務必須能註冊",
        "'{0}' 標了 [{1}]，但{2}", Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ManualRegistration = new(
        "AINX1007", "組合產生器已註冊的類別不可再手動註冊",
        "'{0}' {1}，組合產生器已註冊它；這裡再註冊一次會重複（監控或總覽出現兩次）或與標記的生命週期不一致，刪掉這個呼叫", Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true);
}
