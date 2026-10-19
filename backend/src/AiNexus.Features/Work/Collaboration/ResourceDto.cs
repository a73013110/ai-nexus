namespace AiNexus.Features.Work.Collaboration;

public sealed record ResourceDto(Guid Id, string Name, string Kind, bool CanEdit, bool IsOwner, DateTimeOffset UpdatedAt);
public sealed record DirectoryUserDto(Guid Id, string Account, string DisplayName);
public sealed record DirectoryGroupDto(string Id, string Name);
