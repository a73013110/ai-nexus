namespace AiNexus.Platform.Modules;

/// <summary>
/// 把類別本身註冊為 singleton 服務；組合產生器在編譯期產生註冊（ADR 0033），模組不必再寫，手動註冊會建置失敗（AINX1007）。
/// singleton 不能注入 scoped 服務（例如 DbContext），要用時自建範圍。
/// 和 <see cref="SingletonAttribute{TService}"/> 一起標時，類別本身與各介面是同一個實例。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SingletonAttribute : Attribute;

/// <summary>
/// 以 <typeparamref name="TService"/> 註冊這個類別為 singleton 實作（只標這一個時等同 <c>AddSingleton&lt;TService, 類別&gt;()</c>）；同一個介面可以有多個實作。
/// 一個類別標了多個介面（或同時標 <see cref="SingletonAttribute"/>）時，類別本身也會註冊，各介面轉接到它、共用同一個實例。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class SingletonAttribute<TService> : Attribute where TService : class;
