using AiNexus.Features.Admin.AccessControl.Scopes;

namespace AiNexus.Features.Admin.AccessControl;

/// <summary>目錄項目的種類：頁面與動作是授權（grant），閱讀器只是路由，能不能讀由資源 ACL 決定。</summary>
public enum FeatureKind
{
    Page,
    Action,
    Reader,
}

/// <summary>網址的第一層（ADR 0016），給麵包屑與網址推導用。</summary>
public sealed record AreaDefinition(string Id, string Name);

/// <summary>區底下的模組；<c>Id</c> 是 kebab-case，也是功能 ID 的第一段與網址的第二段。</summary>
public sealed record ModuleDefinition(string Id, AreaDefinition Area, string Name);

/// <summary>目錄裡的一個頁面、動作或閱讀器。<c>Route</c> 由區與 ID 推得；動作沒有網址。</summary>
public sealed record FeatureDefinition(string Id, FeatureKind Kind, ModuleDefinition Module, string Name, string Icon)
{
    public string? Route { get; } = Kind == FeatureKind.Action ? null : $"/{Module.Area.Id}/{Id.Replace('.', '/')}";

    /// <summary>頁面與動作才會寫進 <c>accesscontrol.Features</c> 並能授予；閱讀器不是授權。</summary>
    public bool IsGrant => Kind != FeatureKind.Reader;

    /// <summary>
    /// 支援的資料範圍（<see cref="DataScopes"/>，由窄到寬）。空的表示沒有資料列概念、授權不帶範圍；
    /// 宣告了就表示該功能的查詢經 <see cref="IDataScope"/> 套用範圍，授予時必須選其中一個（ADR 0017）。
    /// </summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];
}

/// <summary>
/// 頁面、動作與閱讀器的唯一來源（ADR 0016）。每個 ID 字面值只在這裡的常數出現一次，
/// 網址由「區＋ID」推得、不另寫字面值；資料庫只存授權、啟用與名稱覆寫。
/// grant（頁面＋動作）要與 <c>database/App/Seed/accesscontrol.sql</c> 一致，<c>FeatureCatalogSeedTests</c> 雙向比對；
/// OpenAPI 以 <see cref="Grants"/> 輸出 <c>FeatureId</c> 列舉，並以 <c>x-feature-catalog</c> 輸出整份目錄給前端產生路由與麵包屑。
/// </summary>
public static class FeatureCatalog
{
    // 單頁模組的 ID 就是模組名（網址省略頁面段）；動作的上層頁面是去掉最後一段的 ID。
    public const string Dashboard = "dashboard";
    public const string Chat = "chat";
    public const string Projects = "projects";
    public const string Files = "files";
    public const string FilesReader = "files.reader";
    public const string Knowledge = "knowledge";
    public const string KnowledgeReader = "knowledge.reader";
    public const string KnowledgeRetrieval = "knowledge.retrieval";
    public const string Artifacts = "artifacts";
    public const string Sharing = "sharing";
    public const string SharingReader = "sharing.reader";
    public const string Jobs = "jobs";
    public const string Repositories = "repositories";
    public const string IdentityUsers = "identity.users";
    public const string AccessControlRoles = "access-control.roles";
    public const string AccessControlGroups = "access-control.groups";
    public const string AccessControlFeatures = "access-control.features";
    public const string AccessControlNavigation = "access-control.navigation";
    public const string Organization = "organization";
    public const string OperationsUsage = "operations.usage";
    public const string BillingPrices = "billing.prices";
    public const string Integrations = "integrations";
    public const string Monitoring = "monitoring";
    public const string Audit = "audit";
    public const string DiagnosticsLogs = "diagnostics.logs";
    public const string DiagnosticsLogsDetail = "diagnostics.logs.detail";
    public const string DiagnosticsLogsExport = "diagnostics.logs.export";
    public const string Quality = "quality";
    public const string QualityRetrieval = "quality.retrieval";
    public const string Design = "design";

    public static readonly AreaDefinition Work = new("work", "AI 工作台");
    public static readonly AreaDefinition Admin = new("admin", "系統管理");

    public static IReadOnlyList<AreaDefinition> Areas { get; } = [Work, Admin];

    // 模組 ID 與單頁功能相同時引用同一個常數，避免同一個字面值寫兩次。
    private static readonly ModuleDefinition DashboardModule = new(Dashboard, Work, "總覽");
    private static readonly ModuleDefinition ChatModule = new(Chat, Work, "對話");
    private static readonly ModuleDefinition ProjectsModule = new(Projects, Work, "專案");
    private static readonly ModuleDefinition FilesModule = new(Files, Work, "檔案庫");
    private static readonly ModuleDefinition KnowledgeModule = new(Knowledge, Work, "知識庫");
    private static readonly ModuleDefinition ArtifactsModule = new(Artifacts, Work, "成果文件");
    private static readonly ModuleDefinition SharingModule = new(Sharing, Work, "分享");
    private static readonly ModuleDefinition JobsModule = new(Jobs, Work, "背景任務");
    private static readonly ModuleDefinition RepositoriesModule = new(Repositories, Work, "程式庫");
    private static readonly ModuleDefinition IdentityModule = new("identity", Admin, "身分");
    private static readonly ModuleDefinition AccessControlModule = new("access-control", Admin, "存取控制");
    private static readonly ModuleDefinition OrganizationModule = new(Organization, Admin, "組織");
    private static readonly ModuleDefinition OperationsModule = new("operations", Admin, "平台營運");
    private static readonly ModuleDefinition BillingModule = new("billing", Admin, "計費");
    private static readonly ModuleDefinition IntegrationsModule = new(Integrations, Admin, "資料來源");
    private static readonly ModuleDefinition MonitoringModule = new(Monitoring, Admin, "即時監控");
    private static readonly ModuleDefinition AuditModule = new(Audit, Admin, "活動稽核");
    private static readonly ModuleDefinition DiagnosticsModule = new("diagnostics", Admin, "診斷");
    private static readonly ModuleDefinition QualityModule = new(Quality, Admin, "品質評測");
    private static readonly ModuleDefinition DesignModule = new(Design, Admin, "介面元件");

    public static IReadOnlyList<ModuleDefinition> Modules { get; } =
    [
        DashboardModule, ChatModule, ProjectsModule, FilesModule, KnowledgeModule, ArtifactsModule, SharingModule, JobsModule, RepositoriesModule,
        IdentityModule, AccessControlModule, OrganizationModule, OperationsModule, BillingModule, IntegrationsModule, MonitoringModule, AuditModule, DiagnosticsModule,
        QualityModule, DesignModule,
    ];

    /// <summary>宣告順序即輸出到 OpenAPI 的順序。圖示名稱必須是前端 <c>shared/ui/icon.ts</c> 已有的名稱。</summary>
    public static IReadOnlyList<FeatureDefinition> All { get; } =
    [
        new(Dashboard, FeatureKind.Page, DashboardModule, "總覽", "dashboard"),
        new(Chat, FeatureKind.Page, ChatModule, "對話", "chat"),
        new(Projects, FeatureKind.Page, ProjectsModule, "專案", "projects"),
        new(Files, FeatureKind.Page, FilesModule, "檔案庫", "files"),
        new(FilesReader, FeatureKind.Reader, FilesModule, "檔案閱讀器", "files"),
        new(Knowledge, FeatureKind.Page, KnowledgeModule, "知識庫", "library"),
        new(KnowledgeReader, FeatureKind.Reader, KnowledgeModule, "文件閱讀器", "document"),
        new(KnowledgeRetrieval, FeatureKind.Page, KnowledgeModule, "檢索管理", "database"),
        new(Artifacts, FeatureKind.Page, ArtifactsModule, "成果文件", "document"),
        new(Sharing, FeatureKind.Page, SharingModule, "分享", "share"),
        new(SharingReader, FeatureKind.Reader, SharingModule, "分享閱讀器", "share"),
        new(Jobs, FeatureKind.Page, JobsModule, "背景任務", "tasks"),
        new(Repositories, FeatureKind.Page, RepositoriesModule, "程式庫", "git"),
        new(IdentityUsers, FeatureKind.Page, IdentityModule, "使用者", "user"),
        new(AccessControlRoles, FeatureKind.Page, AccessControlModule, "角色", "lock"),
        new(AccessControlGroups, FeatureKind.Page, AccessControlModule, "功能群組與模型", "apps"),
        new(AccessControlFeatures, FeatureKind.Page, AccessControlModule, "功能", "list"),
        new(AccessControlNavigation, FeatureKind.Page, AccessControlModule, "選單", "sidebar"),
        new(Organization, FeatureKind.Page, OrganizationModule, "組織單位", "organization"),
        new(OperationsUsage, FeatureKind.Page, OperationsModule, "平台用量", "chart"),
        new(BillingPrices, FeatureKind.Page, BillingModule, "模型與工具價格", "money"),
        new(Integrations, FeatureKind.Page, IntegrationsModule, "資料來源", "integrations"),
        new(Monitoring, FeatureKind.Page, MonitoringModule, "即時監控", "activity"),
        new(Audit, FeatureKind.Page, AuditModule, "活動稽核", "audit") { Scopes = [DataScopes.Own, DataScopes.Unit, DataScopes.UnitTree, DataScopes.All] },
        new(DiagnosticsLogs, FeatureKind.Page, DiagnosticsModule, "系統日誌", "logs"),
        new(DiagnosticsLogsDetail, FeatureKind.Action, DiagnosticsModule, "日誌診斷詳情", "search"),
        new(DiagnosticsLogsExport, FeatureKind.Action, DiagnosticsModule, "日誌匯出", "download"),
        new(Quality, FeatureKind.Page, QualityModule, "品質評測", "shield"),
        new(QualityRetrieval, FeatureKind.Page, QualityModule, "檢索評測", "search"),
        // 介面元件工作臺是純前端頁面、沒有 API；獨立成一個 grant 才能單獨授予。
        new(Design, FeatureKind.Page, DesignModule, "介面元件", "sliders"),
    ];

    /// <summary>可授予的功能（頁面＋動作），即 <c>accesscontrol.Features</c> 應有的列。</summary>
    public static IReadOnlyList<FeatureDefinition> Grants { get; } = [.. All.Where(x => x.IsGrant)];

    private static readonly Dictionary<string, FeatureDefinition> ById = All.ToDictionary(x => x.Id, StringComparer.Ordinal);
    private static readonly Dictionary<string, int> Positions = All.Select((x, i) => (x.Id, i)).ToDictionary(x => x.Id, x => x.i, StringComparer.Ordinal);

    /// <summary>
    /// 功能清單的排序鍵：<see cref="All"/> 的宣告順序（ADR 0016：選單位置在 NavigationItems，功能本身不再有排序欄位）。
    /// 目錄不認得的舊列排在最後，呼叫端再依 ID 排出穩定順序。
    /// </summary>
    public static int Position(string id) => Positions.GetValueOrDefault(id, int.MaxValue);

    /// <summary>
    /// 查目錄。資料庫可能有程式不認得的功能列（舊資料或 fork 移除的功能），呼叫端拿到 null 時要照未知功能處理，不可拋例外。
    /// </summary>
    public static FeatureDefinition? Find(string id) => ById.GetValueOrDefault(id);

    /// <summary>
    /// 頁面或閱讀器的網址，給後端產生前端連結用；不寫死字串，模組換區時連結跟著目錄走。動作或未知 ID 是程式錯誤，直接拋出。
    /// </summary>
    public static string RouteOf(string id)
        => Find(id)?.Route ?? throw new ArgumentException($"'{id}' is not a page or reader in the feature catalog.", nameof(id));
}
