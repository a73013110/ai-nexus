using AiNexus.Generators.Common;
using Microsoft.CodeAnalysis;

namespace AiNexus.Generators.Composition;

/// <summary>一個 <c>IFeatureModule</c> 實作，以及它自己宣告了哪些有預設實作的成員。</summary>
internal sealed record ModuleRegistration
{
    public const string Namespace = "AiNexus.Platform.Modules", Contract = "IFeatureModule";

    private ModuleRegistration(string type, string tag, bool addsServices, bool mapsEndpoints, bool verifiesStartup, DiagnosticInfo? problem)
    {
        Type = type;
        Tag = tag;
        AddsServices = addsServices;
        MapsEndpoints = mapsEndpoints;
        VerifiesStartup = verifiesStartup;
        Problem = problem;
    }

    public string Type { get; }

    /// <summary>OpenAPI tag：類別名去掉 <c>Module</c>。</summary>
    public string Tag { get; }

    public bool AddsServices { get; }

    public bool MapsEndpoints { get; }

    public bool VerifiesStartup { get; }

    /// <summary>不為 null 表示不能產生註冊，只回報這個診斷。</summary>
    public DiagnosticInfo? Problem { get; }

    public static ModuleRegistration? From(INamedTypeSymbol type)
    {
        if (!Symbols.IsConcreteClass(type) || Symbols.Interfaces(type, Namespace, Contract).FirstOrDefault() is not { } contract) return null;
        var problem = Symbols.IsVisibleToAssembly(type) ? null
            : new DiagnosticInfo(CompositionDiagnostics.InvisibleType, LocationInfo.From(type), type.ToDisplayString(), "功能模組");
        var tag = type.Name.EndsWith("Module", StringComparison.Ordinal) ? type.Name.Substring(0, type.Name.Length - "Module".Length) : type.Name;
        return new(Symbols.FullName(type), tag, Declares(type, contract, "AddServices"), Declares(type, contract, "MapEndpoints"), Declares(type, contract, "VerifyStartup"), problem);
    }

    // 有預設實作的 static virtual 成員只能經由受限的型別參數呼叫，模組沒有自己實作時直接呼叫會編譯失敗；
    // 所以只為模組（或其基底類別）真的實作的成員產生呼叫，沒實作的本來就是空的預設。
    private static bool Declares(INamedTypeSymbol type, INamedTypeSymbol contract, string member)
        => contract.GetMembers(member).FirstOrDefault() is { } declared
            && type.FindImplementationForInterfaceMember(declared) is { ContainingType.TypeKind: not TypeKind.Interface };
}
