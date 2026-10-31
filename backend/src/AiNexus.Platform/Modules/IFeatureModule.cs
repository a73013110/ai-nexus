namespace AiNexus.Platform.Modules;

/// <summary>
/// 業務功能模組介面契約。各模組獨立管理其內部服務、設定選項、授權原則與端點路由。
/// 實作這個介面就會被組合產生器（<see cref="FeatureCompositionAttribute"/>）納入，不必修改任何集中清單；模組之間不可依賴註冊順序。
/// </summary>
public interface IFeatureModule
{
    /// <summary>註冊模組專屬之相依服務、設定選項（Options）與授權原則（Policies）。</summary>
    static abstract void AddServices(IHostApplicationBuilder builder);

    /// <summary>將模組端點掛載至 <c>/api/v1</c> 路由群組下；預設要求登入，功能 policy 或匿名存取才需要另外宣告。</summary>
    static virtual void MapEndpoints(RouteGroupBuilder api) { }

    /// <summary>容器建好、對外服務前檢查模組依賴的外部環境（例如儲存目錄）；不健全就丟例外中止啟動，而不是等第一次請求才失敗。</summary>
    static virtual void VerifyStartup(IServiceProvider services) { }
}
