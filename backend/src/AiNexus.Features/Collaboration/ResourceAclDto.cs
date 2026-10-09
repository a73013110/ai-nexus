namespace AiNexus.Features.Collaboration;

public sealed record ResourceMemberDto(Guid UserId, string Account, string DisplayName, string Role);

public sealed record ResourceAclDto(IReadOnlyList<ResourceMemberDto> Members, IReadOnlyList<string> GroupIds);
