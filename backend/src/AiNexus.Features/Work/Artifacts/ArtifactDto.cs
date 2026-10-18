using AiNexus.Features.Work.Collaboration;

namespace AiNexus.Features.Work.Artifacts;

public sealed record ArtifactDto(ResourceDto Resource, int Version, int CurrentVersion, string Content, Guid? SourceMessageId, Guid? ProjectId);
