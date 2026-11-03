using AiNexus.Generators.Common;
using Microsoft.CodeAnalysis;

namespace AiNexus.Generators.Composition;

/// <summary>產生的一個註冊呼叫：<c>{Method}&lt;{TypeArguments}&gt;(services)</c>，名稱都是 <c>global::</c> 開頭的完整名稱。</summary>
internal sealed record ServiceCall
{
    public ServiceCall(string method, string typeArguments)
    {
        Method = method;
        TypeArguments = typeArguments;
    }

    public string Method { get; }

    public string TypeArguments { get; }
}

/// <summary>
/// 類別自己宣告的服務（ADR 0033）：實作了標 <c>[RegisterImplementations]</c> 的介面或 <c>IHostedService</c>，或標了 <c>[Scoped]</c>、<c>[Singleton]</c>。
/// 怎麼註冊寫在 Platform 的擴充方法裡，這裡只決定要呼叫哪些。
/// </summary>
internal sealed record ServiceRegistration
{
    public const string Namespace = "AiNexus.Platform.Modules", ContractAttribute = "RegisterImplementationsAttribute";

    private const string HostingNamespace = "Microsoft.Extensions.Hosting", HostedService = "IHostedService";

    // IHostedService 在框架裡、無法標屬性，是唯一內建的契約；它的註冊方法已經是 TryAddEnumerable。
    private const string AddHostedService = "global::Microsoft.Extensions.DependencyInjection.ServiceCollectionHostedServiceExtensions.AddHostedService";

    private const string AttributeSuffix = "Attribute";

    private ServiceRegistration(string type, EquatableArray<ServiceCall> calls, DiagnosticInfo? problem)
    {
        Type = type;
        Calls = calls;
        Problem = problem;
    }

    public string Type { get; }

    /// <summary>依序產生的呼叫：契約、hosted service，最後是標記的生命週期。</summary>
    public EquatableArray<ServiceCall> Calls { get; }

    public DiagnosticInfo? Problem { get; }

    public static ServiceRegistration? From(INamedTypeSymbol type)
    {
        var declared = type.GetAttributes().Where(x => x.AttributeClass is { } attribute && IsLifetime(attribute)).ToList();
        // 抽象基底類別實作契約很正常（具體的子類別才註冊）；標了生命週期卻不能建立才是錯。
        if (!Symbols.IsConcreteClass(type))
            return declared.Count == 0 ? null : new(Symbols.FullName(type), new([]), Invalid(type, declared[0], "不能由 DI 建立（static、abstract 或開放泛型）"));

        var calls = new List<ServiceCall>();
        var self = Symbols.FullName(type);
        foreach (var (contract, registrar, method) in Contracts(type))
            calls.Add(new($"{Symbols.FullName(registrar)}.{method}", string.Join(", ", contract.TypeArguments.Select(Symbols.FullName).Concat([self]))));
        if (Symbols.Interfaces(type, HostingNamespace, HostedService).Any()) calls.Add(new(AddHostedService, self));

        foreach (var attribute in declared)
        {
            var lifetime = attribute.AttributeClass!.Name.Substring(0, attribute.AttributeClass.Name.Length - AttributeSuffix.Length);
            var method = $"global::{Namespace}.DeclaredServiceRegistration.AddDeclared{lifetime}";
            if (attribute.AttributeClass.TypeArguments.Length == 0)
            {
                calls.Add(new(method, self));
                continue;
            }
            var service = attribute.AttributeClass.TypeArguments[0];
            if (!Symbols.IsAssignableTo(type, service)) return new(self, new([]), Invalid(type, attribute, $"沒有實作 '{service.ToDisplayString()}'"));
            calls.Add(new(method, $"{Symbols.FullName(service)}, {self}"));
        }

        if (calls.Count == 0) return null;
        var problem = Symbols.IsVisibleToAssembly(type) ? null
            : new DiagnosticInfo(CompositionDiagnostics.InvisibleType, LocationInfo.From(type), type.ToDisplayString(), "服務");
        return new(self, new([.. calls]), problem);
    }

    /// <summary>
    /// 型別實作的介面中標了 <c>[RegisterImplementations]</c> 的那些，以及各自的註冊方法。
    /// 產生器據此產生呼叫，<see cref="ManualRegistrationAnalyzer"/> 據此找出重複的手動註冊，兩者必須是同一條規則。
    /// </summary>
    public static IEnumerable<(INamedTypeSymbol Contract, INamedTypeSymbol Registrar, string Method)> Contracts(INamedTypeSymbol type)
    {
        foreach (var contract in type.AllInterfaces)
            foreach (var attribute in contract.OriginalDefinition.GetAttributes())
                if (attribute.AttributeClass is { } attributeClass && Symbols.Is(attributeClass, Namespace, ContractAttribute)
                    && attribute.ConstructorArguments.Length == 2
                    && attribute.ConstructorArguments[0].Value is INamedTypeSymbol registrar && attribute.ConstructorArguments[1].Value is string method)
                    yield return (contract, registrar, method);
    }

    /// <summary>手動呼叫的是不是產生器會為 <paramref name="type"/> 產生的同一個註冊；是的話回傳對應的契約名稱。和 <see cref="From"/> 一樣只看能建立、看得到的類別。</summary>
    public static string? GeneratedContract(IMethodSymbol method, INamedTypeSymbol type)
    {
        if (!Symbols.IsConcreteClass(type) || !Symbols.IsVisibleToAssembly(type)) return null;
        if (method.Name == "AddHostedService" && Symbols.Is(method.ContainingType, "Microsoft.Extensions.DependencyInjection", "ServiceCollectionHostedServiceExtensions"))
            return Symbols.Interfaces(type, HostingNamespace, HostedService).FirstOrDefault()?.ToDisplayString();
        return Contracts(type).Where(x => x.Method == method.Name && SymbolEqualityComparer.Default.Equals(x.Registrar, method.ContainingType))
            .Select(x => x.Contract.ToDisplayString()).FirstOrDefault();
    }

    // [Scoped]、[Singleton] 與各自的 <TService> 版：同名，型別參數數量不同。
    private static bool IsLifetime(INamedTypeSymbol attribute) => attribute is { Name: "ScopedAttribute" or "SingletonAttribute", Arity: <= 1 }
        && attribute.ContainingNamespace.ToDisplayString() == Namespace;

    private static DiagnosticInfo Invalid(INamedTypeSymbol type, AttributeData attribute, string reason)
        => new(CompositionDiagnostics.InvalidService, LocationInfo.From(type), type.ToDisplayString(), attribute.AttributeClass!.ToDisplayString(), reason);
}
