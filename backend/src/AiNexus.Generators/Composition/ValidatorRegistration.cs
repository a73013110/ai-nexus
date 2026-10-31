using System.Collections.Immutable;
using AiNexus.Generators.Common;
using Microsoft.CodeAnalysis;

namespace AiNexus.Generators.Composition;

/// <summary>一個 FluentValidation 驗證器與它實作的每個 <c>IValidator&lt;T&gt;</c>；請求驗證過濾器依 <c>IValidator&lt;T&gt;</c> 解析。</summary>
internal sealed record ValidatorRegistration
{
    private ValidatorRegistration(string type, EquatableArray<string> services, DiagnosticInfo? problem)
    {
        Type = type;
        Services = services;
        Problem = problem;
    }

    public string Type { get; }

    public EquatableArray<string> Services { get; }

    public DiagnosticInfo? Problem { get; }

    public static ValidatorRegistration? From(INamedTypeSymbol type)
    {
        if (!Symbols.IsConcreteClass(type)) return null;
        var services = Symbols.Interfaces(type, "FluentValidation", "IValidator", arity: 1).ToImmutableArray();
        if (services.IsEmpty) return null;
        var problem = Symbols.IsVisibleToAssembly(type) && services.All(Symbols.IsVisibleToAssembly) ? null
            : new DiagnosticInfo(CompositionDiagnostics.InvisibleType, LocationInfo.From(type), type.ToDisplayString(), "請求驗證器");
        return new(Symbols.FullName(type), new([.. services.Select(Symbols.FullName).OrderBy(x => x, StringComparer.Ordinal)]), problem);
    }
}
