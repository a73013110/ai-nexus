namespace AiNexus.Platform.Modules;

/// <summary>
/// 業務功能模組介面契約。各模組獨立管理其內部服務、設定選項、授權原則與端點路由。
/// Host 從明確清單一次組裝所有模組，所以新增功能不需要修改 Program.cs；每個模組的服務註冊也與其程式碼放在一起。
/// </summary>
public interface IFeatureModule
{
    /// <summary>註冊模組專屬之相依服務、設定選項（Options）與授權原則（Policies）。</summary>
    static abstract void AddServices(IHostApplicationBuilder builder);

    /// <summary>將模組端點掛載至 <c>/api/v1</c> 路由群組下；預設要求登入，功能 policy 或匿名存取才需要另外宣告。</summary>
    static virtual void MapEndpoints(RouteGroupBuilder api) { }
}
