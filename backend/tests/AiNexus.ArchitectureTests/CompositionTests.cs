using AiNexus.Features;
using AiNexus.Platform;
using AiNexus.Platform.Events;
using AiNexus.Platform.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.ArchitectureTests;

/// <summary>
/// 組合產生器（ADR 0029）的結果對照執行期反射：每個切片註冊為 scoped，每個 <see cref="RequestValidator{T}"/> 以 <see cref="IRequestValidator"/> 註冊為 singleton。
/// 產生器的辨識規則與這裡的定義分歧時，列出漏掉的型別。每個有處理器的領域事件也要有派送器。
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
        var validators = Assemblies.Features.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } && IsRequestValidator(t)).ToList();
        Assert.NotEmpty(validators);
        var missing = validators.Where(t => !Registered.Value.Any(x => x.ServiceType == typeof(IRequestValidator) && x.ImplementationType == t && x.Lifetime == ServiceLifetime.Singleton))
            .Select(t => t.FullName).Order(StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, "Request validators not registered as IRequestValidator singletons by the composition generator:\n" + string.Join('\n', missing));
    }

    // 派送器在 AddDomainEventHandler 登記；直接註冊 IDomainEventHandler<T> 時事件會被悄悄略過。
    [Fact]
    public void EveryDomainEventHandlerHasADispatcher()
    {
        var events = Registered.Value.Where(x => x.ServiceType.IsGenericType && x.ServiceType.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
            .Select(x => x.ServiceType.GetGenericArguments()[0]).Distinct().ToList();
        Assert.NotEmpty(events);
        var dispatched = Registered.Value.Where(x => x.ServiceType.Name == "IDomainEventDispatcher").Select(x => x.ImplementationType?.GetGenericArguments().Single()).ToHashSet();
        var missing = events.Where(x => !dispatched.Contains(x)).Select(x => x.FullName).Order(StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, "Domain events with handlers but no dispatcher (register handlers with AddDomainEventHandler):\n" + string.Join('\n', missing));
    }

    private static bool IsRequestValidator(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(RequestValidator<>)) return true;
        return false;
    }
}
