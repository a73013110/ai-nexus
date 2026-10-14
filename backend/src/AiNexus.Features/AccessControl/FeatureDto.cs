namespace AiNexus.Features.AccessControl;

/// <summary>A feature as the menu shows it; <c>GroupId</c> null is the top level, an empty <c>Route</c> a grant without a page.</summary>
public sealed record FeatureDto(string Id, string Name, string Route, string? GroupId, string Icon, int SortOrder);
