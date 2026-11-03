using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace AiNexus.Generators.Composition;

/// <summary>
/// 實作了組合擴充點（<c>[RegisterImplementations]</c> 的介面、<c>IHostedService</c>）或標了 <c>[Scoped]</c>、<c>[Singleton]</c> 的類別由組合產生器註冊；
/// 模組再手動註冊時，有些註冊不會去重（監控來源、總覽區塊）會悄悄出現兩次，標記的類別則可能被註冊成另一種生命週期，所以回報 AINX1007。
/// 只看組件內宣告的具體類別：外部組件的型別產生器看不到，本來就要手動註冊。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ManualRegistrationAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [CompositionDiagnostics.ManualRegistration];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        // 產生的註冊本身就是這些呼叫，不檢查。
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterOperationAction(static operation =>
        {
            // 註冊方法的最後一個型別參數是實作；先以泛型與來源篩掉絕大多數的呼叫。
            var method = ((IInvocationOperation)operation.Operation).TargetMethod;
            if (method is not { IsGenericMethod: true, IsStatic: true } || method.TypeArguments.Last() is not INamedTypeSymbol type
                || !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, operation.Compilation.Assembly)) return;
            if (ServiceRegistration.GeneratedBy(method, type) is { } reason)
                operation.ReportDiagnostic(Diagnostic.Create(CompositionDiagnostics.ManualRegistration, operation.Operation.Syntax.GetLocation(), type.ToDisplayString(), reason));
        }, OperationKind.Invocation);
    }
}
