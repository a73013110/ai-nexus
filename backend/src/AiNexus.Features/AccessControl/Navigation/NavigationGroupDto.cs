namespace AiNexus.Features.AccessControl.Navigation;

/// <summary>A menu group; <c>ParentId</c> null is the top level. Features and groups of one level are ordered features first, each by <c>SortOrder</c> then id.</summary>
public sealed record NavigationGroupDto(string Id, string? ParentId, string Name, int SortOrder);
