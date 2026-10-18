using AiNexus.Data.App.Projects;
using AiNexus.Features.Work.Collaboration;

namespace AiNexus.Features.Work.Projects;

public sealed record ProjectDto(ResourceDto Resource, string Description, string Instructions, int Version, bool IsArchived);

internal static class ProjectDtoMapping
{
    public static ProjectDto ToDto(this Project project, ResourceDto resource) => new(resource, project.Description, project.Instructions, project.Version, project.IsArchived);
}
