using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.Platform.Modules;

/// <summary>
/// <see cref="ScopedAttribute"/>、<see cref="SingletonAttribute"/> 的註冊方式；只給組合產生器產生的程式碼呼叫，模組要註冊時直接用 <c>AddScoped</c>、<c>AddSingleton</c>。
/// 產生的註冊排在各模組的 <c>AddServices</c> 之後：類別本身用 TryAdd，模組已註冊同一型別時以模組的為準；
/// 介面用 TryAddEnumerable，同一個介面可以有多個實作，同一個實作只註冊一次。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class DeclaredServiceRegistration
{
    public static IServiceCollection AddDeclaredScoped<TImplementation>(this IServiceCollection services) where TImplementation : class
    {
        services.TryAddScoped<TImplementation>();
        return services;
    }

    public static IServiceCollection AddDeclaredScoped<TService, TImplementation>(this IServiceCollection services)
        where TService : class where TImplementation : class, TService
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<TService, TImplementation>());
        return services;
    }

    public static IServiceCollection AddDeclaredSingleton<TImplementation>(this IServiceCollection services) where TImplementation : class
    {
        services.TryAddSingleton<TImplementation>();
        return services;
    }

    public static IServiceCollection AddDeclaredSingleton<TService, TImplementation>(this IServiceCollection services)
        where TService : class where TImplementation : class, TService
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<TService, TImplementation>());
        return services;
    }
}
