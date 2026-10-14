namespace AiNexus.Features.AccessControl;

public static class BuiltInAccess
{
    public const string MemberRole = "member";
    public const string WorkspaceGroup = "workspace";
    /// <summary>Owned by the Administration module, which seeds the group; administrator-only features are granted to it.</summary>
    public const string AdministratorsGroup = "administrators";
    public const string ChatFeature = "chat";
    public const string ChatPolicy = Policies.Prefix + ChatFeature;
    /// <summary>Menu groups seeded by AccessControl (<c>NavigationGroupConfiguration</c>); modules place their features in them.</summary>
    public const string WorkNavigation = "work", CollaborationNavigation = "collaboration", SystemNavigation = "system";
}
