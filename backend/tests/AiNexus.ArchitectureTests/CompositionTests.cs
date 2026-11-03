using AiNexus.Features;
using AiNexus.Platform;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.ArchitectureTests;

/// <summary>
/// 組合產生器（ADR 0029、0033）的結果對照執行期反射：每個切片註冊為 scoped，每個 <see cref="RequestValidator{T}"/> 以 <see cref="IRequestValidator"/> 註冊為 singleton，
/// 實作組合擴充點的類別與標了生命週期的類別都有註冊。
/// 產生器的辨識規則與這裡的定義分歧時，列出漏掉的型別。
/// </summary>
public sealed class CompositionTests
{
    private static readonly Lazy<List<ServiceDescriptor>> Registered = new(() =>
    {
        // 只看註冊，不建容器也不啟動，所以不需要設定與資料庫。
        var builder = WebApplication.CreateEmptyBuilder(new() { EnvironmentName = "Production" });
        builder.AddPlatform();
        builder.AddFeatures();
        return [.. builder.Services];
    });

    [Fact]
    public void EverySliceIsRegisteredAsScoped()
    {
        var slices = Slices.In(Assemblies.Features).Where(t => !t.IsAbstract).ToList();
        Assert.NotEmpty(slices);
        var missing = slices.Where(t => !Registered.Value.Any(x => x.ServiceType == t && x.ImplementationType == t && x.Lifetime == ServiceLifetime.Scoped))
            .Select(t => t.FullName).Order(StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, "Slices not registered as scoped by the composition generator:\n" + string.Join('\n', missing));
    }

    [Fact]
    public void EveryRequestValidatorIsRegisteredAsASingleton()
    {
        var validators = ConcreteClasses().Where(IsRequestValidator).ToList();
        Assert.NotEmpty(validators);
        var missing = validators.Where(t => !Registered.Value.Any(x => x.ServiceType == typeof(IRequestValidator) && x.ImplementationType == t && x.Lifetime == ServiceLifetime.Singleton))
            .Select(t => t.FullName).Order(StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, "Request validators not registered as IRequestValidator singletons by the composition generator:\n" + string.Join('\n', missing));
        // 過濾器在建置端點時查好驗證器並跨請求共用；模組自行加上的其他 lifetime 註冊也不行。
        var wrong = Registered.Value.Where(x => x.ServiceType == typeof(IRequestValidator) && x.Lifetime != ServiceLifetime.Singleton)
            .Select(x => $"{x.ImplementationType?.FullName} ({x.Lifetime})").Order(StringComparer.Ordinal).ToList();
        Assert.True(wrong.Count == 0, "Request validators must be registered as singletons:\n" + string.Join('\n', wrong));
    }

    /// <summary>註冊方法各自決定形狀（監控來源另外登記型別），共同點是實作型別一定出現在某個註冊裡。</summary>
    [Fact]
    public void EveryContractImplementationIsRegistered()
    {
        var implementations = ConcreteClasses().Where(t => t.GetInterfaces().Any(IsContract)).ToList();
        Assert.NotEmpty(implementations);
        var missing = implementations.Where(t => !Registered.Value.Any(x => x.ServiceType == t || (!x.IsKeyedService && x.ImplementationType == t)))
            .Select(t => t.FullName).Order(StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, "Contract implementations not registered by the composition generator:\n" + string.Join('\n', missing));
    }

    [Fact]
    public void EveryLifetimeMarkerIsRegistered()
    {
        var expected = ConcreteClasses().SelectMany(t => t.GetCustomAttributes(inherit: false).Select(a => Expected(t, a.GetType()))).OfType<Marker>().ToList();
        Assert.NotEmpty(expected);
        var missing = expected.Where(e => !Registered.Value.Any(x => !x.IsKeyedService && x.ServiceType == e.Service && x.ImplementationType == e.Implementation && x.Lifetime == e.Lifetime))
            .Select(e => $"{e.Implementation.FullName} as {e.Service.Name} ({e.Lifetime})").Order(StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, "Lifetime markers not registered by the composition generator:\n" + string.Join('\n', missing));

        static Marker? Expected(Type type, Type attribute)
        {
            var definition = attribute.IsGenericType ? attribute.GetGenericTypeDefinition() : attribute;
            var service = attribute.IsGenericType ? attribute.GetGenericArguments()[0] : type;
            return definition == typeof(ScopedAttribute) || definition == typeof(ScopedAttribute<>) ? new(service, type, ServiceLifetime.Scoped)
                : definition == typeof(SingletonAttribute) || definition == typeof(SingletonAttribute<>) ? new(service, type, ServiceLifetime.Singleton)
                : null;
        }
    }

    private sealed record Marker(Type Service, Type Implementation, ServiceLifetime Lifetime);

    private static IEnumerable<Type> ConcreteClasses() => Assemblies.Features.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false });

    private static bool IsContract(Type contract) => contract == typeof(Microsoft.Extensions.Hosting.IHostedService)
        || (contract.IsGenericType ? contract.GetGenericTypeDefinition() : contract).IsDefined(typeof(RegisterImplementationsAttribute), inherit: false);

    private static bool IsRequestValidator(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(RequestValidator<>)) return true;
        return false;
    }
}
