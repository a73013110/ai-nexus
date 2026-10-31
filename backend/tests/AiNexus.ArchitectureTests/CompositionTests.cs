using AiNexus.Features;
using AiNexus.Platform;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.ArchitectureTests;

/// <summary>
/// 組合產生器（ADR 0029）的結果對照執行期反射：每個切片註冊為 scoped，每個驗證器以 <see cref="IValidator{T}"/> 註冊為 singleton。
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
    public void EveryValidatorIsRegisteredAsASingleton()
    {
        var validators = Assemblies.Features.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .SelectMany(t => t.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>)).Select(i => (Service: i, Type: t)))
            .ToList();
        Assert.NotEmpty(validators);
        var missing = validators.Where(v => !Registered.Value.Any(x => x.ServiceType == v.Service && x.ImplementationType == v.Type && x.Lifetime == ServiceLifetime.Singleton))
            .Select(v => v.Type.FullName).Order(StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, "Validators not registered as IValidator<T> singletons by the composition generator:\n" + string.Join('\n', missing));
    }
}
