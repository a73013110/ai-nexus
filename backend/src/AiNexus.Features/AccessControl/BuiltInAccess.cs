namespace AiNexus.Features.AccessControl;

public static class BuiltInAccess
{
    public const string MemberRole = "member";
    public const string WorkspaceGroup = "workspace";
    public const string ChatFeature = "chat";
    public const string ChatPolicy = Policies.Prefix + ChatFeature;
}
