using AiNexus.Features.AccessControl.Navigation;

namespace AiNexus.Features.AccessControl;

public sealed record AccessItemDto(string Id, string Name);

/// <summary>
/// Effective grants. <c>NavigationGroups</c> holds the menu groups that contain a feature with a page, directly or through
/// a subgroup; it is filled only by <see cref="AccessService.WithNavigationAsync"/> (the payloads that draw a menu), so
/// grant checks do not read it.
/// </summary>
public sealed record AccessDto(IReadOnlyList<AccessItemDto> Roles, IReadOnlyList<AccessItemDto> Groups, IReadOnlyList<FeatureDto> Features, IReadOnlyList<NavigationGroupDto> NavigationGroups);
