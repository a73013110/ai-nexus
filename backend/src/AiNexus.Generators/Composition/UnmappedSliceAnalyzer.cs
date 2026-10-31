using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace AiNexus.Generators.Composition;

/// <summary>
/// 切片由組合產生器自動註冊，但端點要由模組的 <c>MapEndpoints</c> 明確掛載（群組的授權與順序留在模組）。
/// 忘了掛載時編譯照過、端點悄悄不存在，所以每個掛載方法都要在組件內被呼叫或引用，否則回報 AINX1004。
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
            // 沒有引用 ASP.NET Core 路由的組件不會有切片。
            if (start.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Routing.RouteGroupBuilder") is null) return;

            var maps = new ConcurrentBag<IMethodSymbol>();
            var used = new ConcurrentDictionary<IMethodSymbol, bool>(SymbolEqualityComparer.Default);

            start.RegisterSymbolAction(symbol =>
            {
                if (symbol.Symbol is IMethodSymbol method && SliceRegistration.IsMapMethod(method)) maps.Add(method);
            }, SymbolKind.Method);

            // 呼叫與方法群組（傳給委派）都算掛載；先以名稱與 static 篩掉絕大多數的呼叫，不做其他判斷。
            start.RegisterOperationAction(operation =>
            {
                var target = operation.Operation switch
                {
                    IInvocationOperation invocation => invocation.TargetMethod,
                    IMethodReferenceOperation reference => reference.Method,
                    _ => null,
                };
                if (target is { IsStatic: true } && target.Name.StartsWith("Map", StringComparison.Ordinal)) used.TryAdd(target.OriginalDefinition, true);
            }, OperationKind.Invocation, OperationKind.MethodReference);

            start.RegisterCompilationEndAction(end =>
            {
                foreach (var method in maps.Where(x => !used.ContainsKey(x)))
                    end.ReportDiagnostic(Diagnostic.Create(CompositionDiagnostics.UnmappedSlice, method.Locations.FirstOrDefault(),
                        method.ContainingType.ToDisplayString(), method.Name));
            });
        });
    }
}
