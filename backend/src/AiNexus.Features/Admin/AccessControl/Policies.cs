namespace AiNexus.Features.Admin.AccessControl;

/// <summary>
/// 授權 policy 名稱。單一功能的 policy 名稱是 <c>feature:&lt;id&gt;</c>，id 引用 <see cref="FeatureCatalog"/>；
/// 端點一律引用這些常數而非字串字面值，<c>EndpointConventionTests</c> 會檢查每個被引用的 policy 都已註冊。
/// 端點的 policy＝呼叫它的頁面；多個頁面共用的端點用 any-of policy（名稱不對應單一功能），在下面註明原因。
/// </summary>
public static class Policies
{
    public const string Prefix = "feature:";
    public const string Chat = Prefix + FeatureCatalog.Chat;
    public const string Projects = Prefix + FeatureCatalog.Projects;
    public const string Artifacts = Prefix + FeatureCatalog.Artifacts;
    public const string Sharing = Prefix + FeatureCatalog.Sharing;
    public const string Quality = Prefix + FeatureCatalog.Quality;
    public const string QualityRetrieval = Prefix + FeatureCatalog.QualityRetrieval;
    public const string Knowledge = Prefix + FeatureCatalog.Knowledge;
    public const string KnowledgeRetrieval = Prefix + FeatureCatalog.KnowledgeRetrieval;
    public const string Jobs = Prefix + FeatureCatalog.Jobs;
    public const string Integrations = Prefix + FeatureCatalog.Integrations;
    public const string Files = Prefix + FeatureCatalog.Files;
    public const string Dashboard = Prefix + FeatureCatalog.Dashboard;
    public const string Repositories = Prefix + FeatureCatalog.Repositories;
    public const string IdentityUsers = Prefix + FeatureCatalog.IdentityUsers;
    public const string AccessControlRoles = Prefix + FeatureCatalog.AccessControlRoles;
    public const string AccessControlGroups = Prefix + FeatureCatalog.AccessControlGroups;
    public const string AccessControlFeatures = Prefix + FeatureCatalog.AccessControlFeatures;
    public const string AccessControlNavigation = Prefix + FeatureCatalog.AccessControlNavigation;
    public const string Organization = Prefix + FeatureCatalog.Organization;
    public const string OperationsUsage = Prefix + FeatureCatalog.OperationsUsage;
    public const string BillingPrices = Prefix + FeatureCatalog.BillingPrices;
    public const string MailMessages = Prefix + FeatureCatalog.MailMessages;
    public const string MailMessagesResend = Prefix + FeatureCatalog.MailMessagesResend;
    public const string AttendanceCards = Prefix + FeatureCatalog.AttendanceCards;
    public const string AttendanceCardsMaintain = Prefix + FeatureCatalog.AttendanceCardsMaintain;
    public const string AttendanceImports = Prefix + FeatureCatalog.AttendanceImports;
    public const string AttendanceImportsReimport = Prefix + FeatureCatalog.AttendanceImportsReimport;

    /// <summary>對話或成果：兩個入口都可使用的文字工具。</summary>
    public const string Text = Prefix + "text";

    /// <summary>任何可以持有附件的功能：檔案、對話、知識庫或專案。</summary>
    public const string Attachments = Prefix + "attachments";

    /// <summary>
    /// 管理目錄（角色、群組、功能與模型）：使用者頁要角色與群組名稱來編輯指派，存取控制的各頁要整份目錄來編輯
    /// （選單頁要功能名稱與外部連結可選的群組），所以這些頁任一授權都可讀。
    /// </summary>
    public const string AccessCatalog = Prefix + "access-catalog";
}

/// <summary>
/// 原本 <c>admin</c>、<c>operations</c> 兩個管理授權拆出的功能。測試身分可以讀取這些頁面的 API 以重現使用者看到的畫面，
/// 但不可變更（<c>FeatureAuthorizationHandler</c>）；新增同性質的管理頁時加進這裡。
/// </summary>
public static class AdministrativeFeatures
{
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(
    [
        FeatureCatalog.IdentityUsers, FeatureCatalog.AccessControlRoles, FeatureCatalog.AccessControlGroups, FeatureCatalog.AccessControlFeatures,
        FeatureCatalog.AccessControlNavigation, FeatureCatalog.Organization, FeatureCatalog.OperationsUsage, FeatureCatalog.KnowledgeRetrieval, FeatureCatalog.BillingPrices,
        FeatureCatalog.MailMessages, FeatureCatalog.MailMessagesResend, FeatureCatalog.AttendanceCards, FeatureCatalog.AttendanceCardsMaintain,
    ], StringComparer.Ordinal);
}
