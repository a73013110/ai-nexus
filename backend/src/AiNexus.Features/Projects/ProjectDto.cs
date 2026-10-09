using AiNexus.Features.Collaboration;

namespace AiNexus.Features.Projects;

public sealed record ProjectDto(ResourceDto Resource, string Description, string Instructions, int Version, bool IsArchived);
