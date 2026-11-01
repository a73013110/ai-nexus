using System.Collections.Frozen;

namespace AiNexus.Platform.Modules;

/// <summary>
/// 依字串鍵挑一個 <typeparamref name="TService"/> 實作，例如背景工作種類、遠端檔案來源、模型或檢索供應商。
/// 實作以 <see cref="KeyedServiceRegistration.AddKeyedService{TService, TImplementation}"/> 註冊成 keyed service，
/// 查詢只建立查到的那一個，生命週期照它自己的註冊。
/// </summary>
/// <remarks>
/// 本身是 transient，注入的 <see cref="IServiceProvider"/> 是取用端所在的範圍：scoped 實作要由 scoped 取用端（或自建的範圍）查詢，
/// singleton 取用端只能查 singleton 實作，否則範圍驗證會擋下。
/// </remarks>
public sealed class KeyedServices<TService> where TService : class
{
    private readonly IServiceProvider services;
    private readonly KeyedServiceIndex<TService> index;

    internal KeyedServices(IServiceProvider services, KeyedServiceIndex<TService> index)
    {
        this.services = services;
        this.index = index;
    }

    public bool Contains(string key) => index.Keys.Contains(key);

    /// <summary>鍵沒有註冊時回傳 null，由呼叫端決定是回傳錯誤還是略過。</summary>
    public TService? Find(string key) => Contains(key) ? services.GetRequiredKeyedService<TService>(key) : null;

    /// <summary>鍵一定要有實作時使用（設定值已在啟動時檢查過）；沒有就是程式錯誤。</summary>
    public TService Require(string key) =>
        Find(key) ?? throw new InvalidOperationException($"No {typeof(TService).Name} is registered for key '{key}'.");
}

public static class KeyedServiceRegistration
{
    /// <summary>以 <paramref name="key"/> 註冊一個實作，並登記 <see cref="KeyedServices{TService}"/> 查表。鍵在註冊時就知道，查詢不必建立其他實作。</summary>
    public static IServiceCollection AddKeyedService<TService, TImplementation>(this IServiceCollection services, string key, ServiceLifetime lifetime)
        where TService : class where TImplementation : class, TService
    {
        services.Add(new ServiceDescriptor(typeof(TService), key, typeof(TImplementation), lifetime));
        return services.AddKeyedLookup<TService>();
    }

    /// <summary>以工廠註冊一個實作（建構需要額外參數時，例如具名 HttpClient）。</summary>
    public static IServiceCollection AddKeyedService<TService>(this IServiceCollection services, string key, ServiceLifetime lifetime, Func<IServiceProvider, TService> factory)
        where TService : class
    {
        services.Add(new ServiceDescriptor(typeof(TService), key, (provider, _) => factory(provider), lifetime));
        return services.AddKeyedLookup<TService>();
    }

    /// <summary>
    /// 容器建好、對外服務前建立所有查表：同一服務的鍵重複時，DI 只會取最後一個，另一個悄悄失效，所以在啟動時就失敗。
    /// </summary>
    public static void VerifyKeyedServices(this IServiceProvider services) => _ = services.GetServices<IKeyedServiceIndex>().ToArray();

    private static IServiceCollection AddKeyedLookup<TService>(this IServiceCollection services) where TService : class
    {
        if (services.Any(x => x.ServiceType == typeof(KeyedServiceIndex<TService>))) return services;
        // 查表讀的是容器建立時的最終註冊，測試以 RemoveAllKeyed／AddKeyedSingleton 換掉的實作也算在內。
        services.AddSingleton(_ => new KeyedServiceIndex<TService>(services));
        services.AddSingleton<IKeyedServiceIndex>(provider => provider.GetRequiredService<KeyedServiceIndex<TService>>());
        services.AddTransient(provider => new KeyedServices<TService>(provider, provider.GetRequiredService<KeyedServiceIndex<TService>>()));
        return services;
    }
}

internal interface IKeyedServiceIndex;

/// <summary><typeparamref name="TService"/> 已註冊的鍵；建立時檢查鍵都是字串且不重複。</summary>
internal sealed class KeyedServiceIndex<TService> : IKeyedServiceIndex
{
    public FrozenSet<string> Keys { get; }

    public KeyedServiceIndex(IEnumerable<ServiceDescriptor> descriptors)
    {
        var keyed = descriptors.Where(x => x.ServiceType == typeof(TService) && x.IsKeyedService).ToList();
        // AnyKey 或非字串鍵會讓查詢落到預期外的實作；這裡只接受明確的字串鍵。
        if (keyed.FirstOrDefault(x => x.ServiceKey is not string { Length: > 0 }) is { } invalid)
            throw new InvalidOperationException($"{typeof(TService).FullName} keys must be non-empty strings: {Describe(invalid)} uses '{invalid.ServiceKey}'.");
        var duplicates = keyed.GroupBy(x => (string)x.ServiceKey!, StringComparer.Ordinal).Where(g => g.Count() > 1)
            .Select(g => $"'{g.Key}': {string.Join(", ", g.Select(Describe))}").ToList();
        if (duplicates.Count > 0)
            throw new InvalidOperationException($"Each {typeof(TService).FullName} key can be registered only once:\n" + string.Join('\n', duplicates));
        Keys = keyed.Select(x => (string)x.ServiceKey!).ToFrozenSet(StringComparer.Ordinal);
    }

    private static string Describe(ServiceDescriptor x) =>
        x.KeyedImplementationType?.FullName ?? x.KeyedImplementationInstance?.GetType().FullName ?? "factory";
}
