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
}
