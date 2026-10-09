namespace AiNexus.Features.Sharing;

public sealed record ShareDto(Guid Id, string Kind, string Title, string Owner, bool IsOwner, bool IsRevoked, DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt, IReadOnlyList<string> Recipients, bool IncludeAttachments);
