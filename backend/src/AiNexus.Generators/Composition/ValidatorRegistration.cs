using AiNexus.Generators.Common;
using Microsoft.CodeAnalysis;

namespace AiNexus.Generators.Composition;

/// <summary>
/// 一個請求驗證器：繼承 <c>RequestValidator&lt;T&gt;</c> 的類別，以 <c>IRequestValidator</c> 註冊，過濾器依 request 型別查表套用。
/// 單純的 <c>AbstractValidator&lt;T&gt;</c> 是子驗證器，不註冊。
/// </summary>
internal sealed record ValidatorRegistration
{
    public const string Namespace = "AiNexus.Platform.Validation", Base = "RequestValidator";

    private ValidatorRegistration(string type, string requestType, string display, LocationInfo? location, DiagnosticInfo? problem)
    {
        Type = type;
        RequestType = requestType;
        Display = display;
        Location = location;
        Problem = problem;
    }

    public string Type { get; }

    /// <summary>驗證的 request 型別（顯示名稱，只用來比對重複與寫進診斷）；同一型別有兩個驗證器時回報 AINX1005。</summary>
    public string RequestType { get; }

    /// <summary>診斷訊息用的名稱。</summary>
    public string Display { get; }

    /// <summary>重複時回報的位置。同檔裡它上方的編輯會改變位置而讓組合步驟重算，但產出的文字不變。</summary>
    public LocationInfo? Location { get; }

    public DiagnosticInfo? Problem { get; }

    public static ValidatorRegistration? From(INamedTypeSymbol type)
    {
        if (!Symbols.IsConcreteClass(type) || RequestTypeOf(type) is not { } request) return null;
        var problem = Symbols.IsVisibleToAssembly(type) ? null
            : new DiagnosticInfo(CompositionDiagnostics.InvisibleType, LocationInfo.From(type), type.ToDisplayString(), "請求驗證器");
        return new(Symbols.FullName(type), request.ToDisplayString(), type.ToDisplayString(), LocationInfo.From(type), problem);
    }

    private static ITypeSymbol? RequestTypeOf(INamedTypeSymbol type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (Symbols.Is(current, Namespace, Base, arity: 1)) return current.TypeArguments[0];
        return null;
    }
}
