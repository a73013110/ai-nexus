namespace AiNexus.Features.AccessControl;

/// <summary>
/// A feature row and its grant: <see cref="BuiltInAccess.WorkspaceGroup"/> holds it, or
/// <see cref="BuiltInAccess.AdministratorsGroup"/> when it is <c>AdministratorsOnly</c>.
/// </summary>
public sealed record PlatformFeature(string Id, string Name, string Route, int SortOrder, bool AdministratorsOnly = false);
