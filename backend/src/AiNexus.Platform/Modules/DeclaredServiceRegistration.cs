using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.Platform.Modules;

/// <summary>
/// <see cref="ScopedAttribute"/>、<see cref="SingletonAttribute"/> 的註冊方式；只給組合產生器產生的程式碼呼叫。
/// 類別本身用 TryAdd、介面用 TryAddEnumerable：擴充點的註冊方法也註冊了類別本身時不重複，同一個介面仍可有多個實作。
/// 一個類別標了多個標記時，介面以 <c>AddForwarded*</c> 轉接到類別本身，共用同一個實例。
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

    public static IServiceCollection AddForwardedScoped<TService, TImplementation>(this IServiceCollection services)
        where TService : class where TImplementation : class, TService
        => services.AddScoped<TService>(static provider => provider.GetRequiredService<TImplementation>());

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

    public static IServiceCollection AddForwardedSingleton<TService, TImplementation>(this IServiceCollection services)
        where TService : class where TImplementation : class, TService
        => services.AddSingleton<TService>(static provider => provider.GetRequiredService<TImplementation>());
}
