using AiNexus.Features.Work.Sharing.Management;
namespace AiNexus.Features.Work.Sharing;

public sealed record ShareSnapshot(string Content, int? ArtifactVersion, IReadOnlyList<SharedMessageDto> Messages);
