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

    private const string DependencyInjection = "Microsoft.Extensions.DependencyInjection", AttributeSuffix = "Attribute";

    // IHostedService 在框架裡、無法標屬性，是唯一內建的契約；它的註冊方法已經是 TryAddEnumerable。
    private const string AddHostedService = "global::" + DependencyInjection + ".ServiceCollectionHostedServiceExtensions.AddHostedService";

    private const string Registration = "global::" + Namespace + ".DeclaredServiceRegistration";

    private ServiceRegistration(string type, EquatableArray<ServiceCall> calls, DiagnosticInfo? problem)
    {
        Type = type;
        Calls = calls;
        Problem = problem;
    }

    public string Type { get; }

    /// <summary>依序產生的呼叫：契約、hosted service，最後是生命週期標記（類別本身在前、介面在後）。</summary>
    public EquatableArray<ServiceCall> Calls { get; }

    public DiagnosticInfo? Problem { get; }

    public static ServiceRegistration? From(INamedTypeSymbol type)
    {
        var marks = type.GetAttributes().Select(x => x.AttributeClass).OfType<INamedTypeSymbol>().Where(IsLifetime).ToList();
        // 抽象基底類別實作契約很正常（具體的子類別才註冊）；標了生命週期卻不能建立才是錯。
        if (!Symbols.IsConcreteClass(type))
            return marks.Count == 0 ? null : Invalid(type, marks[0], "不能由 DI 建立（static、abstract 或開放泛型）");

        var calls = new List<ServiceCall>();
        var self = Symbols.FullName(type);
        foreach (var (contract, registrar, method) in Contracts(type))
            calls.Add(new($"{Symbols.FullName(registrar)}.{method}", string.Join(", ", contract.TypeArguments.Select(Symbols.FullName).Concat([self]))));
        if (Symbols.Interfaces(type, HostingNamespace, HostedService).Any()) calls.Add(new(AddHostedService, self));

        if (marks.Count > 0)
        {
            foreach (var mark in marks)
            {
                if (mark.Name != marks[0].Name) return Invalid(type, mark, $"也標了 [{Lifetime(marks[0])}]；一個類別只能有一種生命週期");
                if (mark.TypeArguments.FirstOrDefault() is { } service && !Symbols.IsAssignableTo(type, service)) return Invalid(type, mark, $"沒有實作 '{service.ToDisplayString()}'");
            }

            // 只標一個時直接註冊；標了多個時類別本身註冊一次，各介面轉接到它，同一個範圍內拿到的是同一個實例。
            var lifetime = Lifetime(marks[0]);
            var shared = marks.Count > 1;
            if (shared || marks[0].TypeArguments.Length == 0) calls.Add(new($"{Registration}.AddDeclared{lifetime}", self));
            foreach (var service in marks.Where(x => x.TypeArguments.Length == 1).Select(x => Symbols.FullName(x.TypeArguments[0])))
                calls.Add(new($"{Registration}.{(shared ? "AddForwarded" : "AddDeclared")}{lifetime}", $"{service}, {self}"));
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

    /// <summary>
    /// 手動註冊的是不是產生器會為 <paramref name="type"/> 產生的註冊；是的話回傳原因（實作的契約或標記）。
    /// 和 <see cref="From"/> 一樣只看能建立、看得到的類別。標了生命週期的類別再以 DI 的 Add*、TryAdd* 或 <c>ServiceDescriptor</c> 註冊，生命週期可能和標記不一致，也算重複。
    /// </summary>
    public static string? GeneratedBy(IMethodSymbol method, INamedTypeSymbol type)
    {
        if (!Symbols.IsConcreteClass(type) || !Symbols.IsVisibleToAssembly(type)) return null;
        if (method.Name == "AddHostedService" && Symbols.Is(method.ContainingType, DependencyInjection, "ServiceCollectionHostedServiceExtensions"))
            return Symbols.Interfaces(type, HostingNamespace, HostedService).FirstOrDefault() is { } hosted ? $"實作 '{hosted.ToDisplayString()}'" : null;
        if (Symbols.Is(method.ContainingType, DependencyInjection, "ServiceCollectionServiceExtensions")
            || Symbols.Is(method.ContainingType, DependencyInjection, "ServiceDescriptor")
            || Symbols.Is(method.ContainingType, DependencyInjection + ".Extensions", "ServiceCollectionDescriptorExtensions"))
            return type.GetAttributes().Select(x => x.AttributeClass).OfType<INamedTypeSymbol>().FirstOrDefault(IsLifetime) is { } mark ? $"標了 [{Display(mark)}]" : null;
        return Contracts(type).Where(x => x.Method == method.Name && SymbolEqualityComparer.Default.Equals(x.Registrar, method.ContainingType))
            .Select(x => $"實作 '{x.Contract.ToDisplayString()}'").FirstOrDefault();
    }

    // [Scoped]、[Singleton] 與各自的 <TService> 版：同名，型別參數數量不同。
    private static bool IsLifetime(INamedTypeSymbol attribute) => attribute is { Name: "ScopedAttribute" or "SingletonAttribute", Arity: <= 1 }
        && attribute.ContainingNamespace.ToDisplayString() == Namespace;

    private static string Lifetime(INamedTypeSymbol mark) => mark.Name.Substring(0, mark.Name.Length - AttributeSuffix.Length);

    // 寫成程式碼裡的樣子：[Scoped]、[Singleton<IFoo>]。
    private static string Display(INamedTypeSymbol mark)
        => mark.TypeArguments.Length == 0 ? Lifetime(mark) : $"{Lifetime(mark)}<{mark.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}>";

    private static ServiceRegistration Invalid(INamedTypeSymbol type, INamedTypeSymbol mark, string reason)
        => new(Symbols.FullName(type), new([]),
            new DiagnosticInfo(CompositionDiagnostics.InvalidService, LocationInfo.From(type), type.ToDisplayString(), Display(mark), reason));
}
