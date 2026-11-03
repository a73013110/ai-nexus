namespace AiNexus.Platform.Modules;

/// <summary>
/// 把類別本身註冊為 scoped 服務；組合產生器在編譯期產生註冊（ADR 0033），模組不必再寫，手動註冊會建置失敗（AINX1007）。
/// 和 <see cref="ScopedAttribute{TService}"/> 一起標時，類別本身與各介面在同一個範圍內是同一個實例。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ScopedAttribute : Attribute;

/// <summary>
/// 以 <typeparamref name="TService"/> 註冊這個類別為 scoped 實作（只標這一個時等同 <c>AddScoped&lt;TService, 類別&gt;()</c>）；同一個介面可以有多個實作。
/// 一個類別標了多個介面（或同時標 <see cref="ScopedAttribute"/>）時，類別本身也會註冊，各介面轉接到它、共用同一個實例。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class ScopedAttribute<TService> : Attribute where TService : class;
