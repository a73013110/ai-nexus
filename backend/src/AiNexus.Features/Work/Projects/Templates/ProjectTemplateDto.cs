using AiNexus.Data.App.Projects;

namespace AiNexus.Features.Work.Projects.Templates;

public sealed record ProjectTemplateDto(Guid Id, string Title, string Content);

internal static class ProjectTemplateDtoMapping
{
    public static ProjectTemplateDto ToDto(this ProjectTemplate template) => new(template.Id, template.Title, template.Content);
}
