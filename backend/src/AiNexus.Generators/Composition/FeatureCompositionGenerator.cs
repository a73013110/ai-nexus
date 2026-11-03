using System.Collections.Immutable;
using AiNexus.Generators.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNexus.Generators.Composition;

/// <summary>
/// 為標了 <c>[FeatureComposition]</c> 的類別產生組件內所有功能模組、端點切片、請求驗證器（ADR 0029）與類別自己宣告的服務（ADR 0033）的註冊。
/// 新增模組或 use case 只動自己的資料夾，不必維護集中清單；執行期也不必反射掃描組件。
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class FeatureCompositionGenerator : IIncrementalGenerator
{
    public const string AttributeName = "AiNexus.Platform.Modules.FeatureCompositionAttribute";

    /// <summary>pipeline 各步驟的追蹤名稱；測試以它們確認無關的編輯不會讓步驟重算。</summary>
    public const string TargetsStep = "Targets", ModulesStep = "Modules", SlicesStep = "Slices", ValidatorsStep = "Validators", ServicesStep = "Services",
        CompositionStep = "Composition";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.SyntaxProvider
            .ForAttributeWithMetadataName(AttributeName, static (node, _) => node is ClassDeclarationSyntax, static (source, _) => CompositionTarget.From(source))
            .WithTrackingName(TargetsStep)
            .Collect()
            .Select(static (items, _) => Sorted(items, x => x.Display));

        // 模組與驗證器都是有基底清單的類別；兩條 pipeline 各自轉換，語意模型由編譯快取共用。
        var modules = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                static (source, ct) => source.SemanticModel.GetDeclaredSymbol(source.Node, ct) is INamedTypeSymbol type ? ModuleRegistration.From(type) : null)
            .Where(static x => x is not null)
            .Select(static (x, _) => x!)
            .WithTrackingName(ModulesStep)
            .Collect()
            .Select(static (items, _) => Sorted(items, x => x.Type));

        var validators = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                static (source, ct) => source.SemanticModel.GetDeclaredSymbol(source.Node, ct) is INamedTypeSymbol type ? ValidatorRegistration.From(type) : null)
            .Where(static x => x is not null)
            .Select(static (x, _) => x!)
            .WithTrackingName(ValidatorsStep)
            .Collect()
            .Select(static (items, _) => Sorted(items, x => x.Type));

        // 切片以 static Map* 方法辨識；先用語法過濾名稱與 static，只有少數方法需要語意判斷。
        var slices = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => node is MethodDeclarationSyntax { ParameterList.Parameters.Count: > 0, Parent: TypeDeclarationSyntax } method
                    && method.Identifier.ValueText.StartsWith("Map", StringComparison.Ordinal) && method.Modifiers.Any(x => x.ValueText == "static"),
                static (source, ct) => source.SemanticModel.GetDeclaredSymbol(source.Node, ct) is IMethodSymbol method ? SliceRegistration.From(method) : null)
            .Where(static x => x is not null)
            .Select(static (x, _) => x!)
            .WithTrackingName(SlicesStep)
            .Collect()
            .Select(static (items, _) => Sorted(items, x => x.Type));

        // 宣告服務的類別有基底清單（契約）或屬性（生命週期）；partial 類別的屬性可能只在其中一個宣告上，轉換時看完整的符號。
        var services = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => node is ClassDeclarationSyntax declaration && (declaration.BaseList is not null || declaration.AttributeLists.Count > 0),
                static (source, ct) => source.SemanticModel.GetDeclaredSymbol(source.Node, ct) is INamedTypeSymbol type ? ServiceRegistration.From(type) : null)
            .Where(static x => x is not null)
            .Select(static (x, _) => x!)
            .WithTrackingName(ServicesStep)
            .Collect()
            .Select(static (items, _) => Sorted(items, x => x.Type));

        var composition = modules.Combine(slices).Combine(validators).Combine(services)
            .Select(static (x, _) => new FeatureComposition(x.Left.Left.Left, x.Left.Left.Right, x.Left.Right, x.Right))
            .WithTrackingName(CompositionStep);

        context.RegisterSourceOutput(targets.Combine(composition), static (output, source) => Generate(output, source.Left, source.Right));
    }

    private static void Generate(SourceProductionContext output, EquatableArray<CompositionTarget> targets, FeatureComposition composition)
    {
        if (targets.Count == 0) return;
        var target = targets.First();
        foreach (var other in targets.Skip(1))
            output.ReportDiagnostic(new DiagnosticInfo(CompositionDiagnostics.DuplicateTarget, other.Location, other.Display, target.Display).ToDiagnostic());
        foreach (var invalid in targets.Where(x => !x.Valid))
            output.ReportDiagnostic(new DiagnosticInfo(CompositionDiagnostics.InvalidTarget, invalid.Location, invalid.Display).ToDiagnostic());
        foreach (var problem in composition.Problems) output.ReportDiagnostic(problem.ToDiagnostic());
        if (target.Valid) output.AddSource(target.Display + ".g.cs", CompositionEmitter.Emit(target, composition));
    }

    // partial 類別的每個宣告都會轉換一次，所以要去重；排序讓產出與註冊順序不受檔案順序影響。
    private static EquatableArray<T> Sorted<T>(ImmutableArray<T> items, Func<T, string> key) where T : IEquatable<T>
        => new([.. items.Distinct().OrderBy(key, StringComparer.Ordinal)]);
}
