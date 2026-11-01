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
    public const string TargetsStep = "Targets", FieldsStep = "Fields";

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

        context.RegisterSourceOutput(targets.Combine(fields), static (output, source) => Generate(output, source.Left, source.Right));
    }

    private static void Generate(SourceProductionContext output, CatalogTarget target, EquatableArray<CatalogField> fields)
    {
        if (!target.Valid)
        {
            output.ReportDiagnostic(new DiagnosticInfo(CatalogDiagnostics.InvalidTarget, target.Location, target.Display).ToDiagnostic());
            return;
        }
        var matching = fields.Where(x => x.Type == target.ElementType).ToList();
        foreach (var hidden in matching.Where(x => x.Hidden is not null))
            output.ReportDiagnostic(new DiagnosticInfo(CatalogDiagnostics.InvisibleField, hidden.Hidden, hidden.Display, target.Display).ToDiagnostic());

        var writer = new SourceWriter();
        if (target.Namespace is { } ns) writer.Line().Line($"namespace {ns};");
        writer.Line().Open(target.Declaration).Line($"{target.Signature} =>").Line("[");
        foreach (var field in matching.Where(x => x.Hidden is null)) writer.Line($"    {field.Reference},");
        writer.Line("];").Close();
        output.AddSource(target.Display + ".g.cs", writer.ToString());
    }

    // partial 類別的欄位只屬於一個宣告，不會重複；排序與 Type.FullName 加宣告順序相同，產出不受檔案順序影響。
    private static EquatableArray<CatalogField> Sorted(ImmutableArray<CatalogField> items)
        => new([.. items.OrderBy(x => x.Order, StringComparer.Ordinal).ThenBy(x => x.Index)]);
}
