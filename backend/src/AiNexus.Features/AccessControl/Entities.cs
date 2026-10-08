namespace AiNexus.Features.AccessControl;

public sealed class Role
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public sealed class RoleGroup
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public sealed class Feature
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Route { get; set; } = "";
    public int SortOrder { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class UserRole
{
    public Guid UserId { get; set; }
    public string RoleId { get; set; } = "";
}

public sealed class RoleGroupRole
{
    public string RoleId { get; set; } = "";
    public string GroupId { get; set; } = "";
}

public sealed class RoleGroupFeature
{
    public string GroupId { get; set; } = "";
    public string FeatureId { get; set; } = "";
}

public static class BuiltInAccess
{
    public const string MemberRole = "member";
    public const string WorkspaceGroup = "workspace";
    public const string ChatFeature = "chat";
    public const string ChatPolicy = "feature:chat";
}

public sealed record AccessItemDto(string Id, string Name);
public sealed record FeatureDto(string Id, string Name, string Route);
public sealed record AccessDto(IReadOnlyList<AccessItemDto> Roles, IReadOnlyList<AccessItemDto> Groups, IReadOnlyList<FeatureDto> Features);
