namespace AiNexus.Features.AccessControl;

/// <summary>
/// A feature row and its grant: <see cref="BuiltInAccess.WorkspaceGroup"/> holds it, or
/// <see cref="BuiltInAccess.AdministratorsGroup"/> when it is <c>AdministratorsOnly</c>. <c>GroupId</c> is the default
/// menu group (null for the top level) and <c>Icon</c> the frontend icon name; administrators may move or rename it later.
/// </summary>
public sealed record PlatformFeature(string Id, string Name, string Route, int SortOrder, string? GroupId, string Icon, bool AdministratorsOnly = false);
