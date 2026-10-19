using AiNexus.Features.Admin.AccessControl.Navigation;

namespace AiNexus.Features.Admin.AccessControl;

public sealed record AccessItemDto(string Id, string Name);

/// <summary>
/// 有效授權。<c>Navigation</c>（已修剪、攤平的選單樹）只有在 <see cref="AccessService.WithNavigationAsync"/>（繪製選單的 payload）才會填入；
/// 授權檢查不讀它，因此省下選單查詢。
/// </summary>
public sealed record AccessDto(IReadOnlyList<AccessItemDto> Roles, IReadOnlyList<AccessItemDto> Groups, IReadOnlyList<FeatureDto> Features, IReadOnlyList<NavigationNodeDto> Navigation);
