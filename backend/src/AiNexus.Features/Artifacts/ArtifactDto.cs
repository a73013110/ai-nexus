using AiNexus.Features.Collaboration;

namespace AiNexus.Features.Artifacts;

public sealed record ArtifactDto(ResourceDto Resource, int Version, int CurrentVersion, string Content, Guid? SourceMessageId, Guid? ProjectId);
