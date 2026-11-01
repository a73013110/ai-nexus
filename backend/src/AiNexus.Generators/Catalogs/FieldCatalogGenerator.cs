using System.Collections.Immutable;
using AiNexus.Generators.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNexus.Generators.Catalogs;

/// <summary>
/// 為標了 <c>[FieldCatalog]</c> 的方法產生實作：列出組件內 static 類別裡、型別等於回傳元素型別的 public static 欄位（ADR 0030）。
/// 各模組只在自己的類別宣告欄位，集中的清單在編譯期組成，執行期不必以反射掃描組件。
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class FieldCatalogGenerator : IIncrementalGenerator
{
    public const string AttributeName = "AiNexus.Platform.Modules.FieldCatalogAttribute";

    /// <summary>pipeline 各步驟的追蹤名稱；測試以它們確認無關的編輯不會讓步驟重算。</summary>
    public const string TargetsStep = "Targets", FieldsStep = "Fields", CatalogStep = "Catalog";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.SyntaxProvider
            .ForAttributeWithMetadataName(AttributeName, static (node, _) => node is MethodDeclarationSyntax, static (source, _) => CatalogTarget.From(source))
            .WithTrackingName(TargetsStep);

        // 語法先篩出 public static 欄位，語意再確認外層是 static 類別；欄位型別等到與目錄合併時才比對，所以每個目錄共用這一份。
        var fields = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => node is FieldDeclarationSyntax { Parent: ClassDeclarationSyntax } field
                    && field.Modifiers.Any(SyntaxKind.PublicKeyword) && field.Modifiers.Any(SyntaxKind.StaticKeyword),
                static (source, ct) => new EquatableArray<CatalogField>(CatalogField.From(((FieldDeclarationSyntax)source.Node).Declaration.Variables
                    .Select(variable => source.SemanticModel.GetDeclaredSymbol(variable, ct)).OfType<IFieldSymbol>())))
            .SelectMany(static (items, _) => items)
            .WithTrackingName(FieldsStep)
            .Collect()
            .Select(static (items, _) => Sorted(items));

        // 先依每個目錄篩出符合的欄位再交給輸出：其他型別的 public static 欄位增刪時，這一步的結果不變，輸出不必重跑。
        var catalogs = targets.Combine(fields)
            .Select(static (source, _) => Catalog.From(source.Left, source.Right))
            .WithTrackingName(CatalogStep);

        context.RegisterSourceOutput(catalogs, static (output, catalog) => Generate(output, catalog));
    }

    private static void Generate(SourceProductionContext output, Catalog catalog)
    {
        foreach (var diagnostic in catalog.Diagnostics) output.ReportDiagnostic(diagnostic.ToDiagnostic());
        var target = catalog.Target;
        if (target.Problem is not null) return;

        var writer = new SourceWriter();
        if (target.Namespace is { } ns) writer.Line().Line($"namespace {ns};");
        writer.Line().Open(target.Declaration).Line($"{target.Signature} =>").Line("[");
        foreach (var reference in catalog.References) writer.Line($"    {reference},");
        writer.Line("];").Close();
        output.AddSource(target.Display + ".g.cs", writer.ToString());
    }

    // partial 類別的欄位只屬於一個宣告，不會重複；排序與 Type.FullName 加宣告順序相同，產出不受檔案順序影響。
    private static EquatableArray<CatalogField> Sorted(ImmutableArray<CatalogField> items)
        => new([.. items.OrderBy(x => x.Order, StringComparer.Ordinal).ThenBy(x => x.Index)]);
}
