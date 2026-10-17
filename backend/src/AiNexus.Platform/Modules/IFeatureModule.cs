namespace AiNexus.Platform.Modules;

/// <summary>
/// 業務功能模組介面契約。各模組獨立管理其內部服務、設定選項、授權原則與端點路由。
/// Host 宿主層從明確清單一次性組裝各模組，因此新增功能時完全不需要修改 Program.cs，且每個模組的服務註冊皆與其程式碼緊密就近組織。
/// </summary>
public interface IFeatureModule
{
    /// <summary>註冊模組專屬之相依服務、設定選項（Options）與授權原則（Policies）。</summary>
    static abstract void AddServices(IHostApplicationBuilder builder);

    /// <summary>將模組端點掛載至需授權的 <c>/api/v1</c> 路由群組下。</summary>
    static virtual void MapEndpoints(RouteGroupBuilder api) { }

    /// <summary>掛載登入前即可公開存取的端點；各公開端點自行宣告其授權策略。</summary>
    static virtual void MapPublicEndpoints(IEndpointRouteBuilder app) { }
}
