namespace AiNexus.Features.Admin.AccessControl;

/// <summary>
/// 授權 policy。單一功能的 policy 名稱是 <c>feature:&lt;id&gt;</c>，由 <see cref="RequireFeature"/> 組出，<see cref="FeatureCatalog.Grants"/> 的功能都已註冊；
/// 端點的 policy＝呼叫它的頁面；多個頁面共用的端點用 any-of policy（名稱不對應單一功能），在下面註明原因，由需要的模組以 <see cref="AddFeaturePolicy"/> 註冊。
/// </summary>
public static class Policies
{
    public const string Prefix = "feature:";

    /// <summary>對話或成果：兩個入口都可使用的文字工具。</summary>
    public const string Text = Prefix + "text";

    /// <summary>
    /// 可以選模型的頁面：對話，以及有助理工具的功能（出勤紀錄）。模型清單本身已依個人的模型政策過濾，
    /// 新增助理工具的功能時加進 <c>InferenceModule</c> 的註冊。
    /// </summary>
    public const string Models = Prefix + "models";

    /// <summary>任何可以持有附件的功能：檔案、對話、知識庫或專案。</summary>
    public const string Attachments = Prefix + "attachments";

    /// <summary>
    /// 管理目錄（角色、群組、功能與模型）：使用者頁要角色與群組名稱來編輯指派，存取控制的各頁要整份目錄來編輯
    /// （選單頁要功能名稱與外部連結可選的群組），所以這些頁任一授權都可讀。
    /// </summary>
    public const string AccessCatalog = Prefix + "access-catalog";

    /// <summary>已登入且持有任一指定功能的使用者；每個請求都依當下的授權判斷。</summary>
    public static IServiceCollection AddFeaturePolicy(this IServiceCollection services, string policy, params string[] features)
        => services.AddAuthorization(options => options.AddPolicy(policy, p => p.RequireAuthenticatedUser().AddRequirements(new FeatureRequirement(features))));

    /// <summary>端點或群組要求持有 <see cref="FeatureCatalog"/> 的某個功能。</summary>
    public static TBuilder RequireFeature<TBuilder>(this TBuilder builder, string feature) where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(Prefix + feature);
}
