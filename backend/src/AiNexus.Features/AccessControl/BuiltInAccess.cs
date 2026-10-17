namespace AiNexus.Features.AccessControl;

public static class BuiltInAccess
{
    public const string MemberRole = "member";
    public const string WorkspaceGroup = "workspace";
    /// <summary>由系統管理模組寫入並擁有；只限管理者的功能都授權給此群組。</summary>
    public const string AdministratorsGroup = "administrators";
    public const string ChatFeature = "chat";
    public const string ChatPolicy = Policies.Prefix + ChatFeature;
    /// <summary>由 AccessControl 寫入的內建選單分群（<c>NavigationGroupConfiguration</c>）；各模組把自己的功能放進這些分群。</summary>
    public const string WorkNavigation = "work", CollaborationNavigation = "collaboration", SystemNavigation = "system";
}
