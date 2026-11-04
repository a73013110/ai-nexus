using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace AiNexus.Generators.Composition;

/// <summary>
/// 切片由組合產生器自動註冊，但端點要由模組的 <c>MapEndpoints</c> 以 <c>MapSlice&lt;T&gt;()</c> 明確掛載（群組的授權與順序留在模組）。
/// 忘了掛載時編譯照過、端點悄悄不存在，所以每個切片都要在組件內當過 <c>MapSlice</c> 的型別引數，否則回報 AINX1004。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnmappedSliceAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [CompositionDiagnostics.UnmappedSlice];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static start =>
        {
            // 看不到掛載方法的組件不會有切片。
            if (start.Compilation.GetTypeByMetadataName($"{SliceRegistration.Namespace}.{SliceRegistration.Mounts}") is null) return;

            var slices = new ConcurrentBag<INamedTypeSymbol>();
            var mounted = new ConcurrentDictionary<ITypeSymbol, bool>(SymbolEqualityComparer.Default);

            start.RegisterSymbolAction(symbol =>
            {
                if (symbol.Symbol is INamedTypeSymbol type && SliceRegistration.IsSlice(type)) slices.Add(type);
            }, SymbolKind.NamedType);

            // 先以名稱篩掉絕大多數的呼叫，再確認是 EndpointSlices.MapSlice。
            start.RegisterOperationAction(operation =>
            {
                if (operation.Operation is IInvocationOperation { TargetMethod: { Name: SliceRegistration.Mount } target } && SliceRegistration.IsMount(target))
                    mounted.TryAdd(target.TypeArguments[0], true);
            }, OperationKind.Invocation);

            start.RegisterCompilationEndAction(end =>
            {
                foreach (var slice in slices.Where(x => !mounted.ContainsKey(x)))
                    end.ReportDiagnostic(Diagnostic.Create(CompositionDiagnostics.UnmappedSlice, slice.Locations.FirstOrDefault(), slice.ToDisplayString()));
            });
        });
    }
}
