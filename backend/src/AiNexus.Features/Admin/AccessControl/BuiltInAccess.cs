namespace AiNexus.Features.Admin.AccessControl;

/// <summary>
/// 程式依賴的內建角色、群組與選單分群 id，以及程式假設存在的預設關聯。資料列由手寫的 <c>database/App/Seed/accesscontrol.sql</c> 寫入；
/// 改這裡或 Seed 任一邊都要同步另一邊，<c>BuiltInAccessSeedTests</c> 會在範本資料庫上比對。
/// </summary>
public static class BuiltInAccess
{
    /// <summary>每位登入的使用者都會被指派的角色（<c>CurrentUser</c>）。</summary>
    public const string MemberRole = "member";
    /// <summary>啟動時指派給設定的管理員帳號（<c>AdminBootstrap</c>）。</summary>
    public const string AdministratorRole = "administrator";
    public const string WorkspaceGroup = "workspace";
    /// <summary>由系統管理模組寫入並擁有；只限管理者的功能都授權給此群組。</summary>
    public const string AdministratorsGroup = "administrators";
    /// <summary>內建選單分群；Seed 把各模組的功能放進這些分群。</summary>
    public const string WorkNavigation = "work", CollaborationNavigation = "collaboration", SystemNavigation = "system";

    public static IReadOnlyList<string> Roles { get; } = [MemberRole, AdministratorRole];
    public static IReadOnlyList<string> Groups { get; } = [WorkspaceGroup, AdministratorsGroup];
    public static IReadOnlyList<string> NavigationGroups { get; } = [WorkNavigation, CollaborationNavigation, SystemNavigation];

    /// <summary>新使用者拿到 member 後要能對話、管理員帳號要能進存取管理；少了任一條，登入後就沒有可用的功能。</summary>
    public static IReadOnlyList<(string Role, string Group)> RoleGroups { get; } =
        [(MemberRole, WorkspaceGroup), (AdministratorRole, AdministratorsGroup)];
    public static IReadOnlyList<(string Group, string Feature)> GroupFeatures { get; } =
        [(WorkspaceGroup, FeatureIds.Chat), (AdministratorsGroup, FeatureIds.Admin)];
}
