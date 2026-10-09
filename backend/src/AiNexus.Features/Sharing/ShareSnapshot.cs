namespace AiNexus.Features.Sharing;

public sealed record ShareSnapshot(string Content, int? ArtifactVersion, IReadOnlyList<SharedMessageDto> Messages);
