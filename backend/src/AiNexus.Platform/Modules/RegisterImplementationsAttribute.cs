namespace AiNexus.Platform.Modules;

/// <summary>
/// 標在組合擴充點的介面上：Features 裡實作它的每個具體類別，都由組合產生器以 <paramref name="registrar"/> 的 <paramref name="method"/> 註冊（ADR 0033），
/// 模組不必、也不可以再手動呼叫（AINX1007）。
/// </summary>
/// <remarks>
/// <paramref name="method"/> 是 <see cref="IServiceCollection"/> 的 static 擴充方法，型別參數依序是介面的型別引數、最後是實作，
/// 例如 <c>AddMonitoringSource&lt;TSource&gt;</c>、<c>AddDomainEventHandler&lt;TEvent, THandler&gt;</c>；怎麼註冊仍寫在這個方法裡，產生器只負責呼叫。
/// 類別以不同型別引數實作同一個泛型介面多次時，每個各呼叫一次。
/// </remarks>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class RegisterImplementationsAttribute(Type registrar, string method) : Attribute
{
    public Type Registrar { get; } = registrar;

    public string Method { get; } = method;
}
