namespace AiNexus.Features.Admin.AccessControl;

/// <summary>
/// 程式依賴的內建角色、群組與預設選單分群 id，以及程式假設存在的預設關聯。資料列由手寫的 <c>database/App/Seed/accesscontrol.sql</c> 寫入；
/// 改這裡或 Seed 任一邊都要同步另一邊，<c>FeatureCatalogSeedTests</c> 會在範本資料庫上比對。
/// </summary>
public static class BuiltInAccess
{
    /// <summary>每位登入的使用者都會被指派的角色（<c>CurrentUser</c>）。</summary>
    public const string MemberRole = "member";
    /// <summary>啟動時指派給設定的管理員帳號（<c>AdminBootstrap</c>）。</summary>
    public const string AdministratorRole = "administrator";
    public const string WorkspaceGroup = "workspace";
    /// <summary>由系統管理模組寫入並擁有；Seed 授予目錄的每個功能（有範圍的給最寬範圍，<c>FeatureCatalogSeedTests</c> 檢查）。</summary>
    public const string AdministratorsGroup = "administrators";
    /// <summary>
    /// 預設選單樹的分群節點（<c>accesscontrol.NavigationItems</c>，kind＝group）。Seed 把目錄的每個頁面放進這些分群（或最上層），
    /// 頁面節點 id 是 <c>&lt;分群&gt;.&lt;功能 id 的點換成連字號&gt;</c>；程式不依賴頁面節點 id，所以不列在這裡。
    /// </summary>
    public const string WorkNavigation = "work", CollaborationNavigation = "collaboration", SystemNavigation = "system", AccessNavigation = "system.access";

    public static IReadOnlyList<string> Roles { get; } = [MemberRole, AdministratorRole];
    public static IReadOnlyList<string> Groups { get; } = [WorkspaceGroup, AdministratorsGroup];
    public static IReadOnlyList<string> NavigationGroups { get; } = [WorkNavigation, CollaborationNavigation, SystemNavigation, AccessNavigation];

    /// <summary>
    /// 新使用者拿到 member 後要能對話、管理員帳號要能進存取管理的各頁（含選單）；少了任一條，登入後就沒有可用的功能，或管理員無法再授權。
    /// administrators 擁有全部功能，另由 <c>FeatureCatalogSeedTests</c> 對整份目錄檢查，不逐條列在這裡。
    /// </summary>
    public static IReadOnlyList<(string Role, string Group)> RoleGroups { get; } =
        [(MemberRole, WorkspaceGroup), (AdministratorRole, AdministratorsGroup)];
    public static IReadOnlyList<(string Group, string Feature)> GroupFeatures { get; } = [(WorkspaceGroup, FeatureCatalog.Chat)];
}
