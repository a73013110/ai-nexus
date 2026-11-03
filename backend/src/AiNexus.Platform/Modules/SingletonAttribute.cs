namespace AiNexus.Platform.Modules;

/// <summary>
/// 把類別本身註冊為 singleton 服務；組合產生器在編譯期產生註冊（ADR 0033），模組的 <c>AddServices</c> 不必再寫。
/// 模組若自己註冊了同一個型別，以模組的為準。singleton 不能注入 scoped 服務（例如 DbContext），要用時自建範圍。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SingletonAttribute : Attribute;

/// <summary>
/// 以 <typeparamref name="TService"/> 註冊這個類別為 singleton 實作（等同 <c>AddSingleton&lt;TService, 類別&gt;()</c>），只註冊介面、不註冊類別本身。
/// 同時標了 <see cref="SingletonAttribute"/> 時會是兩個實例；要共用同一個實例時，在模組以工廠轉接。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class SingletonAttribute<TService> : Attribute where TService : class;
