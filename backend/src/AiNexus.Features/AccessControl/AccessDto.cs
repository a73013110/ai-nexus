namespace AiNexus.Features.AccessControl;

public sealed record AccessItemDto(string Id, string Name);

public sealed record AccessDto(IReadOnlyList<AccessItemDto> Roles, IReadOnlyList<AccessItemDto> Groups, IReadOnlyList<FeatureDto> Features);
