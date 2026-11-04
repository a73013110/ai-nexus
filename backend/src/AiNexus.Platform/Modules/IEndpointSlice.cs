namespace AiNexus.Platform.Modules;

/// <summary>
/// 一個 use case 的端點（垂直切片）。實作的類別由組合產生器註冊為 scoped，再由模組以 <see cref="EndpointSlices.MapSlice{TSlice}"/> 掛到自己的群組；
/// 群組帶著功能 policy、body 上限與 OpenAPI 順序，所以切片只宣告動詞、相對路徑與動作層級的設定。漏掛時建置失敗（AINX1004）。
/// </summary>
/// <remarks>一個切片只對應一個授權情境：同一份邏輯要掛在不同 policy 的群組時拆成兩個切片，共用的部分放模組內的服務。</remarks>
public interface IEndpointSlice
{
    /// <summary>把這個切片的端點掛到 <paramref name="group"/>；同一資源有多個端點時都寫在這裡。</summary>
    static abstract void MapEndpoints(RouteGroupBuilder group);
}

/// <summary>模組掛載切片用的擴充方法。</summary>
public static class EndpointSlices
{
    /// <summary>把切片的端點掛到群組，回傳同一個群組，讓模組一個群組寫成一串。</summary>
    public static RouteGroupBuilder MapSlice<TSlice>(this RouteGroupBuilder group) where TSlice : IEndpointSlice
    {
        TSlice.MapEndpoints(group);
        return group;
    }
}
