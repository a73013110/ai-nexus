using AiNexus.Features.AccessControl;
using AiNexus.Features.Collaboration;
using AiNexus.Platform.Events;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Conversations;

/// <summary>
/// 使用者私人對話與訊息管理模組。其他模組要寫入或推進訊息樹，應透過 <see cref="ConversationService"/>，
/// 由它統一處理分支、串流增量與軟刪除過濾。
/// </summary>
/// <remarks>
/// 備份匯入的請求主體上限為 8MB（<see cref="ImportBodyLimit"/>），防止超大 JSON 耗盡伺服器記憶體。
/// 所有對話端點都套用 <see cref="FeatureIds.Chat"/> 授權策略。
/// </remarks>
public sealed class ConversationsModule : IFeatureModule
{
    /// <summary>
    /// 對話備份匯入的 HTTP 請求主體上限（8 MB）。需能容納 400 則訊息、總計 100 萬字元內容的完整 JSON 備份（上限見 ConversationBackupValidator），
    /// 並預留 JSON 跳脫與分支結構的空間。
    /// </summary>
    public const long ImportBodyLimit = 8 * 1024 * 1024;

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddScoped<ConversationService>();

        // 同一個類別同時是專案刪除的事件處理常式與刪除前檢查：生成中的對話會阻擋專案刪除。
        services.AddDomainEventHandler<ContainerDeleted, DetachDeletedProjectConversations>();
        services.AddScoped<IContainerDeletionCheck, DetachDeletedProjectConversations>();

        services.AddFeaturePolicy(FeatureIds.Chat);
    }

    /// <summary>註冊對話 REST 端點群組；註冊順序即 OpenAPI 發佈順序。</summary>
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/conversations")
            .RequireAuthorization(Policies.Chat)
            .WithTags("Conversations");

        ListConversations.Map(routes);
        ListConversationLabels.Map(routes);
        ImportConversation.Map(routes);
        CreateConversation.Map(routes);
        RenameConversation.Map(routes);
        UpdateConversationSettings.Map(routes);
        SelectBranch.Map(routes);
    }

    /// <summary>管理端稽核式讀取任一使用者的對話。由 <c>FeatureModules</c> 與其他管理端點一起掛載，理由見該處註解。</summary>
    public static void MapAdministration(RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin").RequireAuthorization(Policies.Admin).WithTags("Administration");
        ListAdminUserConversations.Map(admin);
        ReadAdminConversation.Map(admin);
    }
}
